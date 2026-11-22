# Public knowledge library

Learners browse `/knowledge`, read `/knowledge/{slug}`, and revisit saved lessons in `/profile#saved-lessons`.
Knowledge articles, documents and model answers use the existing admin article/category/tag system.
Only Published articles are public. No separate document type or file upload is introduced.

## API

- `GET /api/v1/knowledge/articles`: `q` (title search, max 200), `skill`, `categoryId`, `tag` (max 64), `sort`, `pageNumber` (default 1), `pageSize` (default 12, max 100). Returns the existing article page shape. Status is always Published, regardless of query input.
- `GET /api/v1/knowledge/categories`: categories containing published articles, with published article counts.
- `GET /api/v1/knowledge/articles/{slug}`: published article detail; unavailable articles return 404.
- `GET /api/v1/knowledge/articles/{slug}/related`: up to three other published articles, ranked by common tags, matching category, then newest publication.
- `GET /api/v1/knowledge/media/{id}`: image only when referenced by a published article.
- `GET /api/v1/knowledge/saved`: authenticated user's saved article URLs and availability.
- `PUT /api/v1/knowledge/saved/{articleId}`: idempotently save a published article.
- `DELETE /api/v1/knowledge/saved/{articleId}`: idempotently remove the caller's bookmark.

Saved URLs are derived from the article slug. User identity comes from the bearer token, never a caller-supplied user ID. Unpublished bookmarks show an unavailable label without exposing draft titles. Deleted articles cascade-delete bookmarks.
The Next.js BFF exposes matching `/api/knowledge/*` paths and checks session and same-origin mutations for saved lessons.

## Migration

`20261121085849_AddKnowledgeBookmarks` adds `knowledge_bookmarks` with a composite `(user_id, article_id)` key, save timestamp and cascading foreign keys. No existing data is modified. The migration has been generated but not applied.

From the backend root, apply using the intended environment's connection configuration:

```sh
dotnet ef database update --project src/TOEICMaster.Infrastructure --startup-project src/TOEICMaster.Api
```

API integration tests require a dedicated PostgreSQL connection in `TOEICMASTER_TEST_POSTGRES`.
