-- Applied to the hosted project as migration 20261030075302.
set local lock_timeout = '5s';

alter table public.community_wallpapers
add column if not exists content_hash text;

do $$
begin
  if not exists (
    select 1
    from pg_constraint
    where conname = 'community_wallpapers_content_hash_format'
      and conrelid = 'public.community_wallpapers'::regclass
  ) then
    alter table public.community_wallpapers
    add constraint community_wallpapers_content_hash_format
    check (content_hash is null or content_hash ~ '^[0-9a-f]{64}$');
  end if;
end
$$;

create unique index if not exists community_wallpapers_active_content_hash_key
on public.community_wallpapers (content_hash)
where content_hash is not null
  and status in ('draft', 'published', 'hidden');

grant insert (content_hash) on table public.community_wallpapers to authenticated;
