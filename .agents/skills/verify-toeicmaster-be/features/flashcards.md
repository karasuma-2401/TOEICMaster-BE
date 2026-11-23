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

- `GET /api/v1/flashcards/decks` returns all available flashcard decks.
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
  Observable result: HTTP 200 returning an array of decks with `id`, `slug`, `title`, and `totalCards`.

- Inspect deck by slug:
  ```bash
  curl -s -X GET http://127.0.0.1:15100/api/v1/flashcards/decks/toeic-essential-words \
    -H "Authorization: Bearer $TOKEN"
  ```
  Observable result: HTTP 200 returning deck metadata along with the card list.

- Create a custom deck:
  ```bash
  curl -s -X POST http://127.0.0.1:15100/api/v1/flashcards/decks \
    -H "Authorization: Bearer $TOKEN" \
    -H "Content-Type: application/json" \
    -d '{"title":"Office Business Terms","category":"Business","description":"Common workplace vocabulary"}'
  ```
  Observable result: HTTP 201 Created returning the newly generated deck ID and slug.

- Add a card to the custom deck:
  ```bash
  curl -s -X POST "http://127.0.0.1:15100/api/v1/flashcards/decks/${DECK_ID}/cards" \
    -H "Authorization: Bearer $TOKEN" \
    -H "Content-Type: application/json" \
    -d '{"term":"Negotiation","definition":"Discussion aimed at reaching an agreement","exampleSentence":"The contract was settled after hours of negotiation."}'
  ```
  Observable result: HTTP 201 Created returning card ID and ordinal position.

- Update card study progress:
  ```bash
  curl -s -X PUT "http://127.0.0.1:15100/api/v1/flashcards/cards/${CARD_ID}/progress" \
    -H "Authorization: Bearer $TOKEN" \
    -H "Content-Type: application/json" \
    -d '{"status":"Mastered","starred":true}'
  ```
  Observable result: HTTP 200 confirming the updated mastery status.

## Gotchas

- Built-in decks owned by the system cannot be modified or deleted by individual users.
- Deck slugs must be unique across all public and user decks.
- Recording match scores requires positive integers for `bestTimeMs` and `bestCombo`.
- Unauthenticated requests to flashcard progress return HTTP 401 Unauthorized.
