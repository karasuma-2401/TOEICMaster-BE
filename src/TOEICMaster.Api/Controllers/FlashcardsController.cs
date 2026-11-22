using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TOEICMaster.Api.Extensions;
using TOEICMaster.Application.Flashcards;

namespace TOEICMaster.Api.Controllers;

[ApiController]
[Route("api/v1/flashcards")]
public sealed class FlashcardsController(ISender sender) : ControllerBase
{
    [HttpGet("decks")]
    [AllowAnonymous]
    [ProducesResponseType<FlashcardWorkspaceDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<FlashcardWorkspaceDto>> GetWorkspace(
        CancellationToken cancellationToken)
    {
        var userId = TryGetUserId();
        var result = await sender.Send(new GetFlashcardWorkspaceQuery(userId), cancellationToken);
        return Ok(result);
    }

    [HttpGet("decks/{slug}")]
    [AllowAnonymous]
    [ProducesResponseType<FlashcardDeckDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FlashcardDeckDto>> GetDeckBySlug(
        string slug,
        CancellationToken cancellationToken)
    {
        var userId = TryGetUserId();
        var result = await sender.Send(new GetFlashcardDeckBySlugQuery(slug, userId), cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost("decks")]
    [Authorize]
    [ProducesResponseType<FlashcardDeckDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<FlashcardDeckDto>> CreateDeck(
        [FromBody] CreateFlashcardDeckRequest request,
        CancellationToken cancellationToken)
    {
        var userId = TryGetUserId();
        if (!userId.HasValue)
            return Unauthorized();

        var created = await sender.Send(new CreateFlashcardDeckCommand(userId.Value, request), cancellationToken);
        return CreatedAtAction(nameof(GetDeckBySlug), new { slug = created.Slug }, created);
    }

    [HttpPost("decks/{deckId:guid}/cards")]
    [Authorize]
    [ProducesResponseType<FlashcardCardDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FlashcardCardDto>> AddCard(
        Guid deckId,
        [FromBody] CreateFlashcardCardRequest request,
        CancellationToken cancellationToken)
    {
        var userId = TryGetUserId();
        if (!userId.HasValue)
            return Unauthorized();

        var created = await sender.Send(
            new AddFlashcardCardCommand(userId.Value, deckId, request),
            cancellationToken);

        return created is null ? NotFound() : StatusCode(StatusCodes.Status201Created, created);
    }

    [HttpDelete("decks/{deckId:guid}")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteDeck(
        Guid deckId,
        CancellationToken cancellationToken)
    {
        var userId = TryGetUserId();
        if (!userId.HasValue)
            return Unauthorized();

        var deleted = await sender.Send(new DeleteFlashcardDeckCommand(userId.Value, deckId), cancellationToken);
        return deleted ? NoContent() : NotFound();
    }

    [HttpPut("cards/{cardId:guid}/progress")]
    [Authorize]
    [ProducesResponseType<CardProgressDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CardProgressDto>> UpsertProgress(
        Guid cardId,
        [FromBody] UpsertFlashcardProgressRequest request,
        CancellationToken cancellationToken)
    {
        var userId = TryGetUserId();
        if (!userId.HasValue)
            return Unauthorized();

        var updated = await sender.Send(
            new UpsertFlashcardProgressCommand(userId.Value, cardId, request),
            cancellationToken);

        return updated is null ? NotFound() : Ok(updated);
    }

    [HttpDelete("decks/{deckId:guid}/progress")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ResetDeckProgress(
        Guid deckId,
        CancellationToken cancellationToken)
    {
        var userId = TryGetUserId();
        if (!userId.HasValue)
            return Unauthorized();

        var reset = await sender.Send(
            new ResetFlashcardDeckProgressCommand(userId.Value, deckId),
            cancellationToken);

        return reset ? NoContent() : NotFound();
    }

    [HttpPost("decks/{deckId:guid}/match-records")]
    [Authorize]
    [ProducesResponseType<MatchRecordDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MatchRecordDto>> RecordMatch(
        Guid deckId,
        [FromBody] RecordFlashcardMatchRequest request,
        CancellationToken cancellationToken)
    {
        var userId = TryGetUserId();
        if (!userId.HasValue)
            return Unauthorized();

        var result = await sender.Send(
            new RecordFlashcardMatchCommand(userId.Value, deckId, request),
            cancellationToken);

        return result is null ? NotFound() : Ok(result);
    }

    private Guid? TryGetUserId() => User.TryGetUserId();
}
