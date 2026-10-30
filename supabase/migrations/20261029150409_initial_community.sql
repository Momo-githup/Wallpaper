-- Applied to the hosted project as migration 20261029150409.
create schema if not exists private;

revoke all on schema private from public, anon, authenticated;

create table public.profiles (
    id uuid primary key references auth.users(id) on delete cascade,
    display_name text not null check (char_length(display_name) between 1 and 50),
    avatar_path text,
    bio text check (bio is null or char_length(bio) <= 500),
    created_at timestamptz not null default now(),
    updated_at timestamptz not null default now()
);

create table public.community_wallpapers (
    id bigint generated always as identity primary key,
    owner_id uuid not null references public.profiles(id) on delete cascade,
    title text not null check (char_length(title) between 1 and 100),
    description text check (description is null or char_length(description) <= 2000),
    storage_path text not null unique,
    thumbnail_path text not null,
    width integer not null check (width > 0 and width <= 8192),
    height integer not null check (height > 0 and height <= 8192),
    file_size bigint not null check (file_size > 0 and file_size <= 26214400),
    mime_type text not null check (mime_type in ('image/jpeg', 'image/png', 'image/webp')),
    status text not null default 'published' check (status in ('draft', 'published', 'hidden', 'removed')),
    download_count bigint not null default 0 check (download_count >= 0),
    created_at timestamptz not null default now(),
    updated_at timestamptz not null default now()
);

create table public.wallpaper_likes (
    wallpaper_id bigint not null references public.community_wallpapers(id) on delete cascade,
    user_id uuid not null references public.profiles(id) on delete cascade,
    created_at timestamptz not null default now(),
    primary key (wallpaper_id, user_id)
);

create table public.wallpaper_favorites (
    wallpaper_id bigint not null references public.community_wallpapers(id) on delete cascade,
    user_id uuid not null references public.profiles(id) on delete cascade,
    created_at timestamptz not null default now(),
    primary key (wallpaper_id, user_id)
);

create table public.wallpaper_downloads (
    id bigint generated always as identity primary key,
    wallpaper_id bigint not null references public.community_wallpapers(id) on delete cascade,
    user_id uuid not null references public.profiles(id) on delete cascade,
    created_at timestamptz not null default now()
);

create table public.wallpaper_reports (
    id bigint generated always as identity primary key,
    wallpaper_id bigint not null references public.community_wallpapers(id) on delete cascade,
    reporter_id uuid not null references public.profiles(id) on delete cascade,
    reason text not null check (reason in ('copyright', 'inappropriate', 'spam', 'other')),
    details text check (details is null or char_length(details) <= 1000),
    status text not null default 'pending' check (status in ('pending', 'reviewed', 'dismissed', 'actioned')),
    created_at timestamptz not null default now(),
    unique (wallpaper_id, reporter_id)
);

create table public.tags (
    id bigint generated always as identity primary key,
    name text not null unique check (char_length(name) between 1 and 30),
    created_at timestamptz not null default now()
);

create table public.wallpaper_tags (
    wallpaper_id bigint not null references public.community_wallpapers(id) on delete cascade,
    tag_id bigint not null references public.tags(id) on delete cascade,
    primary key (wallpaper_id, tag_id)
);

create index community_wallpapers_owner_id_idx on public.community_wallpapers (owner_id);
create index community_wallpapers_feed_idx on public.community_wallpapers (created_at desc) where status = 'published';
create index wallpaper_likes_user_id_idx on public.wallpaper_likes (user_id);
create index wallpaper_favorites_user_id_idx on public.wallpaper_favorites (user_id);
create index wallpaper_downloads_wallpaper_id_idx on public.wallpaper_downloads (wallpaper_id);
create index wallpaper_downloads_user_id_idx on public.wallpaper_downloads (user_id);
create index wallpaper_reports_wallpaper_id_idx on public.wallpaper_reports (wallpaper_id);
create index wallpaper_reports_reporter_id_idx on public.wallpaper_reports (reporter_id);
create index wallpaper_tags_tag_id_idx on public.wallpaper_tags (tag_id);

create function private.set_updated_at()
returns trigger
language plpgsql
set search_path = ''
as $$
begin
    new.updated_at = now();
    return new;
end;
$$;

create trigger profiles_set_updated_at
before update on public.profiles
for each row execute function private.set_updated_at();

create trigger community_wallpapers_set_updated_at
before update on public.community_wallpapers
for each row execute function private.set_updated_at();

create function private.handle_new_user()
returns trigger
language plpgsql
security definer
set search_path = ''
as $$
begin
    insert into public.profiles (id, display_name)
    values (
        new.id,
        coalesce(
            nullif(left(trim(new.raw_user_meta_data ->> 'display_name'), 50), ''),
            nullif(left(split_part(new.email, '@', 1), 50), ''),
            '用户'
        )
    )
    on conflict (id) do nothing;
    return new;
end;
$$;

revoke execute on function private.set_updated_at() from public, anon, authenticated;
revoke execute on function private.handle_new_user() from public, anon, authenticated;

create trigger on_auth_user_created
after insert on auth.users
for each row execute function private.handle_new_user();

alter table public.profiles enable row level security;
alter table public.community_wallpapers enable row level security;
alter table public.wallpaper_likes enable row level security;
alter table public.wallpaper_favorites enable row level security;
alter table public.wallpaper_downloads enable row level security;
alter table public.wallpaper_reports enable row level security;
alter table public.tags enable row level security;
alter table public.wallpaper_tags enable row level security;

create policy profiles_read_authenticated on public.profiles
for select to authenticated using (true);

create policy profiles_update_own on public.profiles
for update to authenticated
using ((select auth.uid()) = id)
with check ((select auth.uid()) = id);

create policy community_wallpapers_read on public.community_wallpapers
for select to authenticated
using (status = 'published' or (select auth.uid()) = owner_id);

create policy community_wallpapers_insert_own on public.community_wallpapers
for insert to authenticated
with check ((select auth.uid()) = owner_id);

create policy community_wallpapers_update_own on public.community_wallpapers
for update to authenticated
using ((select auth.uid()) = owner_id)
with check ((select auth.uid()) = owner_id);

create policy community_wallpapers_delete_own on public.community_wallpapers
for delete to authenticated
using ((select auth.uid()) = owner_id);

create policy wallpaper_likes_read_authenticated on public.wallpaper_likes
for select to authenticated using (true);

create policy wallpaper_likes_insert_own on public.wallpaper_likes
for insert to authenticated
with check ((select auth.uid()) = user_id);

create policy wallpaper_likes_delete_own on public.wallpaper_likes
for delete to authenticated
using ((select auth.uid()) = user_id);

create policy wallpaper_favorites_read_own on public.wallpaper_favorites
for select to authenticated
using ((select auth.uid()) = user_id);

create policy wallpaper_favorites_insert_own on public.wallpaper_favorites
for insert to authenticated
with check ((select auth.uid()) = user_id);

create policy wallpaper_favorites_delete_own on public.wallpaper_favorites
for delete to authenticated
using ((select auth.uid()) = user_id);

create policy wallpaper_downloads_read_own on public.wallpaper_downloads
for select to authenticated
using ((select auth.uid()) = user_id);

create policy wallpaper_downloads_insert_own on public.wallpaper_downloads
for insert to authenticated
with check ((select auth.uid()) = user_id);

create policy wallpaper_reports_read_own on public.wallpaper_reports
for select to authenticated
using ((select auth.uid()) = reporter_id);

create policy wallpaper_reports_insert_own on public.wallpaper_reports
for insert to authenticated
with check ((select auth.uid()) = reporter_id);

create policy tags_read_authenticated on public.tags
for select to authenticated using (true);

create policy wallpaper_tags_read_visible on public.wallpaper_tags
for select to authenticated
using (exists (
    select 1 from public.community_wallpapers wallpaper
    where wallpaper.id = wallpaper_id
));

create policy wallpaper_tags_insert_by_owner on public.wallpaper_tags
for insert to authenticated
with check (exists (
    select 1 from public.community_wallpapers wallpaper
    where wallpaper.id = wallpaper_id
      and wallpaper.owner_id = (select auth.uid())
));

create policy wallpaper_tags_delete_by_owner on public.wallpaper_tags
for delete to authenticated
using (exists (
    select 1 from public.community_wallpapers wallpaper
    where wallpaper.id = wallpaper_id
      and wallpaper.owner_id = (select auth.uid())
));

revoke all on table public.profiles from anon;
revoke all on table public.community_wallpapers from anon;
revoke all on table public.wallpaper_likes from anon;
revoke all on table public.wallpaper_favorites from anon;
revoke all on table public.wallpaper_downloads from anon;
revoke all on table public.wallpaper_reports from anon;
revoke all on table public.tags from anon;
revoke all on table public.wallpaper_tags from anon;

grant usage on schema public to authenticated;
grant select, update on table public.profiles to authenticated;
grant select, insert, update, delete on table public.community_wallpapers to authenticated;
grant select, insert, delete on table public.wallpaper_likes to authenticated;
grant select, insert, delete on table public.wallpaper_favorites to authenticated;
grant select, insert on table public.wallpaper_downloads to authenticated;
grant select, insert on table public.wallpaper_reports to authenticated;
grant select on table public.tags to authenticated;
grant select, insert, delete on table public.wallpaper_tags to authenticated;

grant usage, select on sequence public.community_wallpapers_id_seq to authenticated;
grant usage, select on sequence public.wallpaper_downloads_id_seq to authenticated;
grant usage, select on sequence public.wallpaper_reports_id_seq to authenticated;
grant usage, select on sequence public.tags_id_seq to authenticated;
