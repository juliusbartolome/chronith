using Chronith.Application.Behaviors;
using FluentAssertions;
using MediatR;

namespace Chronith.Tests.Unit.Application.Behaviors;

public sealed class SanitizationBehaviorTests
{
    // ── Test request types ─────────────────────────────────────────────────

    private sealed class MutableRequest : IRequest<string>
    {
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int Count { get; set; }
        public Guid Id { get; set; }
        public bool IsActive { get; set; }
    }

    // Record with init-only properties (typical command pattern)
    private sealed record InitOnlyRequest : IRequest<string>
    {
        public required string Title { get; init; }
        public required string Body { get; init; }
    }

    // ── Tests ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenStringPropertyHasHtmlTags_StripsTheTags()
    {
        // Arrange
        var request = new MutableRequest { Name = "<script>alert('xss')</script>Hello" };
        var behavior = new SanitizationBehavior<MutableRequest, string>();
        RequestHandlerDelegate<string> next = _ => Task.FromResult("ok");

        // Act
        var result = await behavior.Handle(request, next, CancellationToken.None);

        // Assert
        result.Should().Be("ok");
        request.Name.Should().Be("Hello");
    }

    [Fact]
    public async Task Handle_WhenStringPropertyHasNoHtml_LeavesValueUnchanged()
    {
        // Arrange
        var request = new MutableRequest { Name = "John Doe" };
        var behavior = new SanitizationBehavior<MutableRequest, string>();
        RequestHandlerDelegate<string> next = _ => Task.FromResult("ok");

        // Act
        await behavior.Handle(request, next, CancellationToken.None);

        // Assert
        request.Name.Should().Be("John Doe");
    }

    [Fact]
    public async Task Handle_WhenNonStringProperties_AreNotModified()
    {
        // Arrange
        var id = Guid.NewGuid();
        var request = new MutableRequest { Count = 42, Id = id, IsActive = true, Name = "plain" };
        var behavior = new SanitizationBehavior<MutableRequest, string>();
        RequestHandlerDelegate<string> next = _ => Task.FromResult("ok");

        // Act
        await behavior.Handle(request, next, CancellationToken.None);

        // Assert
        request.Count.Should().Be(42);
        request.Id.Should().Be(id);
        request.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WhenNullStringProperty_DoesNotThrow()
    {
        // Arrange
        var request = new MutableRequest { Name = "valid", Description = null };
        var behavior = new SanitizationBehavior<MutableRequest, string>();
        RequestHandlerDelegate<string> next = _ => Task.FromResult("ok");

        // Act
        Func<Task> act = async () => await behavior.Handle(request, next, CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync();
        request.Description.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WhenNestedHtmlTags_AllTagsAreStripped()
    {
        // Arrange
        var request = new MutableRequest
        {
            Name = "<div><b>Bold</b> and <i>italic</i></div>",
            Description = "<p>Paragraph <span>text</span></p>"
        };
        var behavior = new SanitizationBehavior<MutableRequest, string>();
        RequestHandlerDelegate<string> next = _ => Task.FromResult("ok");

        // Act
        await behavior.Handle(request, next, CancellationToken.None);

        // Assert
        request.Name.Should().Be("Bold and italic");
        request.Description.Should().Be("Paragraph text");
    }

    [Fact]
    public async Task Handle_WithInitOnlyRecordProperties_DoesNotThrow()
    {
        // Arrange — init-only properties cannot be mutated; behavior must skip silently
        var request = new InitOnlyRequest { Title = "<b>Title</b>", Body = "<p>Body</p>" };
        var behavior = new SanitizationBehavior<InitOnlyRequest, string>();
        RequestHandlerDelegate<string> next = _ => Task.FromResult("ok");

        // Act
        Func<Task> act = async () => await behavior.Handle(request, next, CancellationToken.None);

        // Assert — must not throw; init-only properties are silently skipped
        await act.Should().NotThrowAsync();
        // The values remain unchanged since init-only can't be set via reflection
        request.Title.Should().Be("<b>Title</b>");
        request.Body.Should().Be("<p>Body</p>");
    }

    [Fact]
    public async Task Handle_CallsNextDelegate()
    {
        // Arrange
        var request = new MutableRequest { Name = "test" };
        var behavior = new SanitizationBehavior<MutableRequest, string>();
        var nextCalled = false;
        RequestHandlerDelegate<string> next = _ =>
        {
            nextCalled = true;
            return Task.FromResult("result");
        };

        // Act
        var result = await behavior.Handle(request, next, CancellationToken.None);

        // Assert
        nextCalled.Should().BeTrue();
        result.Should().Be("result");
    }

    [Fact]
    public async Task Handle_WithAttributeStyleHtml_StripsEntireTag()
    {
        // Arrange
        var request = new MutableRequest
        {
            Name = "<a href=\"javascript:void(0)\">Click me</a>"
        };
        var behavior = new SanitizationBehavior<MutableRequest, string>();
        RequestHandlerDelegate<string> next = _ => Task.FromResult("ok");

        // Act
        await behavior.Handle(request, next, CancellationToken.None);

        // Assert
        request.Name.Should().Be("Click me");
    }
}
