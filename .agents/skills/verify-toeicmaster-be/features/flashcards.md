# Flashcards

This feature covers vocabulary study via built-in and user-created flashcard decks.
Learners browse categorized word lists, review cards with audio pronunciations, track spaced learning progress, reset study statistics, and record speed matching game scores.

## Sub-features

- `FC-LIST`: List public and personal flashcard decks grouped by category.
- `FC-VIEW`: Retrieve deck details including card count, slug, and study progress.
- `FC-CREATE`: Create a custom user flashcard deck.
- `FC-CARD`: Add vocabulary cards with terms, definitions, and example sentences.
- `FC-PROG`: Update individual card review status (Learning, Mastered, Starred).
- `FC-RESET`: Reset deck study progress for a fresh learning cycle.
- `FC-MATCH`: Record best match game completion times and combo streaks.

## How to get to it (user POV)

- `GET /api/v1/flashcards/decks` returns all available flashcard decks in a workspace.
- `GET /api/v1/flashcards/decks/{slug}` retrieves a single deck and its cards.
- `POST /api/v1/flashcards/decks` creates a new custom deck for the user.
- `POST /api/v1/flashcards/decks/{deckId}/cards` adds a card to the deck.
- `DELETE /api/v1/flashcards/decks/{deckId}` deletes a personal flashcard deck.
- `PUT /api/v1/flashcards/cards/{cardId}/progress` updates card mastery state and starred flag.
- `DELETE /api/v1/flashcards/decks/{deckId}/progress` clears study progress for all cards in a deck.
- `POST /api/v1/flashcards/decks/{deckId}/match-records` saves high scores for the match game.

## Driving it with HTTP (curl)

Preconditions:
The API instance must be running on `http://127.0.0.1:15100` and an access token `$TOKEN` must be obtained.

- Retrieve flashcard decks:
  ```bash
  curl -s -X GET http://127.0.0.1:15100/api/v1/flashcards/decks \
    -H "Authorization: Bearer $TOKEN"
  ```
  Observable result: HTTP 200 returning `FlashcardWorkspaceDto` containing `decks`, `progress`, and `matchRecords`, where each deck includes `id`, `slug`, `title`, and `cards`.

- Inspect deck by slug:
  ```bash
  curl -s -X GET http://127.0.0.1:15100/api/v1/flashcards/decks/office-operations \
    -H "Authorization: Bearer $TOKEN"
  ```
  Observable result: HTTP 200 returning `FlashcardDeckDto` metadata along with the `cards` list.

- Create a custom deck:
  ```bash
  curl -s -X POST http://127.0.0.1:15100/api/v1/flashcards/decks \
    -H "Authorization: Bearer $TOKEN" \
    -H "Content-Type: application/json" \
    -d '{"title":"Custom Office Terms","description":"Common workplace vocabulary","cards":[{"term":"Negotiation","phonetic":"/nɪˌɡoʊ.ʃiˈeɪ.ʃən/","partOfSpeech":"noun","meaningVi":"sự đàm phán","exampleEn":"The contract was settled after hours of negotiation.","exampleVi":"Hợp đồng đã được dàn xếp sau nhiều giờ đàm phán."}]}'
  ```
  Observable result: HTTP 201 Created returning `FlashcardDeckDto` with generated `id`, `slug`, and `cards`.

- Add a card to the custom deck:
  ```bash
  curl -s -X POST "http://127.0.0.1:15100/api/v1/flashcards/decks/${DECK_ID}/cards" \
    -H "Authorization: Bearer $TOKEN" \
    -H "Content-Type: application/json" \
    -d '{"term":"Collaborate","phonetic":"/kəˈlæb.ə.reɪt/","partOfSpeech":"verb","meaningVi":"hợp tác","exampleEn":"Teams collaborate across regions.","exampleVi":"Các nhóm hợp tác xuyên khu vực."}'
  ```
  Observable result: HTTP 201 Created returning `FlashcardCardDto` with card `id`, `term`, and `meaningVi`.

- Update card study progress:
  ```bash
  curl -s -X PUT "http://127.0.0.1:15100/api/v1/flashcards/cards/${CARD_ID}/progress" \
    -H "Authorization: Bearer $TOKEN" \
    -H "Content-Type: application/json" \
    -d '{"status":"Mastered","starred":true}'
  ```
  Observable result: HTTP 200 returning `CardProgressDto` with `status: "Mastered"`, `starred: true`, and `reviewCount`.

- Record speed matching game score:
  ```bash
  curl -s -X POST "http://127.0.0.1:15100/api/v1/flashcards/decks/${DECK_ID}/match-records" \
    -H "Authorization: Bearer $TOKEN" \
    -H "Content-Type: application/json" \
    -d '{"elapsedMs":45000,"maxCombo":8}'
  ```
  Observable result: HTTP 200 returning `MatchRecordDto` with `bestTimeMs`, `bestCombo`, and `playCount`.

## Gotchas

- Built-in decks owned by the system cannot be modified or deleted by individual users.
- Deck slugs must be unique across all public and user decks.
- Recording match scores requires positive integers for `elapsedMs` and `maxCombo` in `RecordFlashcardMatchRequest`.
- Unauthenticated requests to flashcard progress return HTTP 401 Unauthorized.
