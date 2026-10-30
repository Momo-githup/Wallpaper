-- Applied to the hosted project as migration 20261030081635.
set local lock_timeout = '5s';

alter table public.community_wallpapers
add column if not exists media_type text not null default 'image';

do $$
begin
  if not exists (
    select 1
    from pg_constraint
    where conname = 'community_wallpapers_media_type_check'
      and conrelid = 'public.community_wallpapers'::regclass
  ) then
    alter table public.community_wallpapers
    add constraint community_wallpapers_media_type_check
    check (media_type in ('image', 'video'));
  end if;
end
$$;

alter table public.community_wallpapers
drop constraint if exists community_wallpapers_width_check,
drop constraint if exists community_wallpapers_height_check,
drop constraint if exists community_wallpapers_file_size_check,
drop constraint if exists community_wallpapers_mime_type_check;

do $$
begin
  if not exists (
    select 1
    from pg_constraint
    where conname = 'community_wallpapers_media_metadata_check'
      and conrelid = 'public.community_wallpapers'::regclass
  ) then
    alter table public.community_wallpapers
    add constraint community_wallpapers_media_metadata_check
    check (
      (
        media_type = 'image'
        and width between 1 and 8192
        and height between 1 and 8192
        and file_size between 1 and 26214400
        and mime_type in ('image/jpeg', 'image/png', 'image/webp')
      )
      or
      (
        media_type = 'video'
        and width between 0 and 8192
        and height between 0 and 8192
        and file_size between 1 and 52428800
        and mime_type = 'video/mp4'
      )
    );
  end if;
end
$$;

grant insert (media_type) on table public.community_wallpapers to authenticated;

update storage.buckets
set file_size_limit = 52428800,
    allowed_mime_types = array['image/jpeg', 'image/png', 'image/webp', 'video/mp4']
where id = 'wallpapers';
