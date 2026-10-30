import { createClient } from "npm:@supabase/supabase-js@2.95.0";

const jsonHeaders = { "Content-Type": "application/json" };

function jsonResponse(status: number, body: Record<string, unknown>) {
  return new Response(JSON.stringify(body), { status, headers: jsonHeaders });
}

Deno.serve(async (request: Request) => {
  if (request.method !== "POST") {
    return jsonResponse(405, { error: "仅支持 POST 请求。" });
  }

  const authorization = request.headers.get("Authorization");
  const token = authorization?.replace(/^Bearer\s+/i, "");
  if (!token) {
    return jsonResponse(401, { error: "登录状态已失效，请重新登录。" });
  }

  let payload: { wallpaperId?: number; cleanupRemoved?: boolean };
  try {
    payload = await request.json();
  } catch {
    return jsonResponse(400, { error: "请求内容无效。" });
  }

  const supabaseUrl = Deno.env.get("SUPABASE_URL");
  const serviceRoleKey = Deno.env.get("SUPABASE_SERVICE_ROLE_KEY");
  if (!supabaseUrl || !serviceRoleKey) {
    console.error("Missing Supabase Edge Function environment variables.");
    return jsonResponse(500, { error: "云端撤下服务配置不完整。" });
  }

  const admin = createClient(supabaseUrl, serviceRoleKey, {
    auth: { persistSession: false, autoRefreshToken: false },
  });
  const { data: authData, error: authError } = await admin.auth.getUser(token);
  if (authError || !authData.user) {
    return jsonResponse(401, { error: "登录状态已失效，请重新登录。" });
  }

  if (payload.cleanupRemoved === true) {
    const { data: removedWallpapers, error: removedQueryError } = await admin
      .from("community_wallpapers")
      .select("storage_path, thumbnail_path")
      .eq("owner_id", authData.user.id)
      .eq("status", "removed");
    if (removedQueryError) {
      console.error("Failed to query removed wallpapers", removedQueryError);
      return jsonResponse(500, { error: "查询历史撤下文件失败。" });
    }

    const removedPaths = (removedWallpapers ?? [])
      .flatMap((wallpaper) => [wallpaper.storage_path, wallpaper.thumbnail_path])
      .filter((path): path is string => typeof path === "string" && path.length > 0);
    for (let offset = 0; offset < removedPaths.length; offset += 1000) {
      const { error: cleanupError } = await admin.storage
        .from("wallpapers")
        .remove(removedPaths.slice(offset, offset + 1000));
      if (cleanupError) {
        console.error("Failed to clean removed wallpaper objects", cleanupError);
        return jsonResponse(500, { error: "清理历史撤下文件失败，请稍后重试。" });
      }
    }
    return jsonResponse(200, { success: true, cleanedPaths: removedPaths.length });
  }

  if (!Number.isSafeInteger(payload.wallpaperId) || Number(payload.wallpaperId) <= 0) {
    return jsonResponse(400, { error: "壁纸编号无效。" });
  }

  const { data: wallpaper, error: selectError } = await admin
    .from("community_wallpapers")
    .select("id, owner_id, storage_path, thumbnail_path, status")
    .eq("id", payload.wallpaperId)
    .maybeSingle();

  if (selectError) {
    console.error("Failed to read wallpaper record", selectError);
    return jsonResponse(500, { error: "读取壁纸信息失败。" });
  }
  if (!wallpaper || wallpaper.owner_id !== authData.user.id) {
    return jsonResponse(404, { error: "壁纸不存在，或当前账户无权撤下。" });
  }

  const paths = [wallpaper.storage_path, wallpaper.thumbnail_path]
    .filter((path): path is string => typeof path === "string" && path.length > 0);
  const { error: removeError } = await admin.storage.from("wallpapers").remove(paths);
  if (removeError) {
    console.error("Failed to remove wallpaper objects", removeError);
    return jsonResponse(500, { error: "删除云端壁纸文件失败，请稍后重试。" });
  }

  const { error: updateError } = await admin
    .from("community_wallpapers")
    .update({ status: "removed" })
    .eq("id", wallpaper.id)
    .eq("owner_id", authData.user.id);
  if (updateError) {
    console.error("Files removed but record update failed", updateError);
    return jsonResponse(500, { error: "云端文件已删除，但撤下状态更新失败，请刷新后重试。" });
  }

  return jsonResponse(200, { success: true });
});
