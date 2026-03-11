using Chronith.Application.Behaviors;
using FluentAssertions;
using MediatR;

namespace Chronith.Tests.Unit.Application.Behaviors;

public sealed class SanitizationBehaviorTests
{
    // ── Test request types ─────────────────────────────────────────────────

    // MutableRequest uses regular (non-init) setters to exercise the property-setter path.
    // Real application commands use required init-only properties (see InitOnlyRequest/InitOnlyTestCommand below).
    // After the backing-field fix, the behavior sanitizes both mutable and init-only string properties.
    // MutableRequest exercises the non-init path; InitOnlyTestCommand exercises the backing-field path.
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

    // Request with a computed getter-only string property — must never be written to.
    private sealed class ComputedGetterRequest : IRequest<string>
    {
        public string First { get; set; } = string.Empty;
        public string Last { get; set; } = string.Empty;
        public string Full => $"{First} {Last}";
    }

    private sealed record InitOnlyTestCommand : IRequest<string>
    {
        public required string Name { get; init; }
        public required string Description { get; init; }
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
        // Arrange
        var request = new InitOnlyRequest { Title = "<b>Title</b>", Body = "<p>Body</p>" };
        var behavior = new SanitizationBehavior<InitOnlyRequest, string>();
        RequestHandlerDelegate<string> next = _ => Task.FromResult("ok");

        // Act
        Func<Task> act = async () => await behavior.Handle(request, next, CancellationToken.None);

        // Assert — must not throw; init-only properties are sanitized via backing field
        await act.Should().NotThrowAsync();
        request.Title.Should().Be("Title");
        request.Body.Should().Be("Body");
    }

    [Fact]
    public async Task Handle_StripsTags_FromInitOnlyProperties()
    {
        var behavior = new SanitizationBehavior<InitOnlyTestCommand, string>();
        var cmd = new InitOnlyTestCommand
        {
            Name = "<script>alert(1)</script>Hello",
            Description = "<b>Bold</b> text"
        };

        await behavior.Handle(cmd, _ => Task.FromResult(cmd.Name), CancellationToken.None);

        cmd.Name.Should().Be("Hello");
        cmd.Description.Should().Be("Bold text");
    }

    [Fact]
    public async Task Handle_StripsDangerousBlock_FromInitOnlyProperties()
    {
        var behavior = new SanitizationBehavior<InitOnlyTestCommand, string>();
        var cmd = new InitOnlyTestCommand
        {
            Name = "<style>body{display:none}</style>Safe",
            Description = "Normal"
        };

        await behavior.Handle(cmd, _ => Task.FromResult(""), CancellationToken.None);

        cmd.Name.Should().Be("Safe");
        cmd.Description.Should().Be("Normal");
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

    [Fact]
    public async Task Handle_WithGetterOnlyStringProperty_DoesNotThrow()
    {
        // Arrange — computed getter-only property has no setter; behavior must skip it silently
        var request = new ComputedGetterRequest { First = "<b>John</b>", Last = "Doe" };
        var behavior = new SanitizationBehavior<ComputedGetterRequest, string>();
        RequestHandlerDelegate<string> next = _ => Task.FromResult("ok");

        // Act
        Func<Task> act = async () => await behavior.Handle(request, next, CancellationToken.None);

        // Assert — must not throw; getter-only properties must be skipped
        await act.Should().NotThrowAsync();
        // Settable properties are still sanitized
        request.First.Should().Be("John");
        request.Last.Should().Be("Doe");
    }
}
