-- Applied to the hosted project as migration 20261029150928.
revoke update on table public.profiles from authenticated;
grant update (display_name, avatar_path, bio) on table public.profiles to authenticated;

revoke insert, update on table public.community_wallpapers from authenticated;
grant insert (
    owner_id,
    title,
    description,
    storage_path,
    thumbnail_path,
    width,
    height,
    file_size,
    mime_type,
    status
) on table public.community_wallpapers to authenticated;
grant update (
    title,
    description,
    storage_path,
    thumbnail_path,
    width,
    height,
    file_size,
    mime_type,
    status
) on table public.community_wallpapers to authenticated;

revoke insert on table public.wallpaper_likes from authenticated;
grant insert (wallpaper_id, user_id) on table public.wallpaper_likes to authenticated;

revoke insert on table public.wallpaper_favorites from authenticated;
grant insert (wallpaper_id, user_id) on table public.wallpaper_favorites to authenticated;

revoke insert on table public.wallpaper_downloads from authenticated;
grant insert (wallpaper_id, user_id) on table public.wallpaper_downloads to authenticated;

revoke insert on table public.wallpaper_reports from authenticated;
grant insert (wallpaper_id, reporter_id, reason, details) on table public.wallpaper_reports to authenticated;
