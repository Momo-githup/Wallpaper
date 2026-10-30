insert into storage.buckets (id, name, public, file_size_limit, allowed_mime_types)
values (
    'wallpapers',
    'wallpapers',
    true,
    26214400,
    array['image/jpeg', 'image/png', 'image/webp']
)
on conflict (id) do nothing;

create policy wallpapers_insert_own_folder
on storage.objects
for insert
to authenticated
with check (
    bucket_id = 'wallpapers'
    and (storage.foldername(name))[1] = (select auth.uid())::text
);

create policy wallpapers_delete_own
on storage.objects
for delete
to authenticated
using (
    bucket_id = 'wallpapers'
    and owner_id = (select auth.uid())::text
);
