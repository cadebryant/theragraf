using FluentAssertions;
using Theragraf.Functions.Helpers;

namespace Theragraf.Tests.Helpers;

public class ClientIdHelperTests
{
    // ── Namespace ─────────────────────────────────────────────────────────────

    [Fact]
    public void Namespace_WithEmail_PrependsEightCharHexPrefix()
    {
        var result = ClientIdHelper.Namespace("alice@example.com", "patient-001");

        result.Should().MatchRegex(@"^[0-9a-f]{8}~patient-001$");
    }

    [Fact]
    public void Namespace_IsDeterministic_SameEmailProducesSamePrefix()
    {
        var first  = ClientIdHelper.Namespace("alice@example.com", "patient-001");
        var second = ClientIdHelper.Namespace("alice@example.com", "patient-001");

        first.Should().Be(second);
    }

    [Fact]
    public void Namespace_IsCaseInsensitive_OnEmail()
    {
        var lower = ClientIdHelper.Namespace("alice@example.com", "patient-001");
        var upper = ClientIdHelper.Namespace("ALICE@EXAMPLE.COM", "patient-001");

        lower.Should().Be(upper);
    }

    [Fact]
    public void Namespace_DifferentEmails_ProduceDifferentPrefixes()
    {
        var alice = ClientIdHelper.Namespace("alice@example.com", "patient-001");
        var bob   = ClientIdHelper.Namespace("bob@example.com",   "patient-001");

        alice.Should().NotBe(bob);
    }

    [Fact]
    public void Namespace_NullEmail_ReturnsRawId()
    {
        var result = ClientIdHelper.Namespace(null, "patient-001");
        result.Should().Be("patient-001");
    }

    [Fact]
    public void Namespace_EmptyEmail_ReturnsRawId()
    {
        var result = ClientIdHelper.Namespace("  ", "patient-001");
        result.Should().Be("patient-001");
    }

    [Fact]
    public void Namespace_AlreadyNamespaced_IsIdempotent()
    {
        var first  = ClientIdHelper.Namespace("alice@example.com", "patient-001");
        var second = ClientIdHelper.Namespace("alice@example.com", first);

        second.Should().Be(first);
    }

    // ── StripPrefix ───────────────────────────────────────────────────────────

    [Fact]
    public void StripPrefix_NamespacedId_ReturnsRawSegment()
    {
        var namespaced = ClientIdHelper.Namespace("alice@example.com", "patient-001");
        ClientIdHelper.StripPrefix(namespaced).Should().Be("patient-001");
    }

    [Fact]
    public void StripPrefix_UnprefixedId_ReturnsSameValue()
    {
        ClientIdHelper.StripPrefix("demo-client").Should().Be("demo-client");
    }

    [Fact]
    public void StripPrefix_OnlySeparator_ReturnsEmptyString()
    {
        // Edge case: id is just the separator character
        ClientIdHelper.StripPrefix("~").Should().Be(string.Empty);
    }

    // ── IsNamespaced ──────────────────────────────────────────────────────────

    [Fact]
    public void IsNamespaced_NamespacedId_ReturnsTrue()
    {
        var id = ClientIdHelper.Namespace("alice@example.com", "patient-001");
        ClientIdHelper.IsNamespaced(id).Should().BeTrue();
    }

    [Fact]
    public void IsNamespaced_DemoId_ReturnsFalse()
    {
        ClientIdHelper.IsNamespaced("demo-client-001").Should().BeFalse();
    }

    // ── IsOwner ───────────────────────────────────────────────────────────────

    [Fact]
    public void IsOwner_MatchingEmail_ReturnsTrue()
    {
        var clientId = ClientIdHelper.Namespace("alice@example.com", "patient-001");
        ClientIdHelper.IsOwner("alice@example.com", clientId).Should().BeTrue();
    }

    [Fact]
    public void IsOwner_MatchingEmail_IsCaseInsensitive()
    {
        var clientId = ClientIdHelper.Namespace("alice@example.com", "patient-001");
        ClientIdHelper.IsOwner("ALICE@EXAMPLE.COM", clientId).Should().BeTrue();
    }

    [Fact]
    public void IsOwner_DifferentEmail_ReturnsFalse()
    {
        var clientId = ClientIdHelper.Namespace("alice@example.com", "patient-001");
        ClientIdHelper.IsOwner("bob@example.com", clientId).Should().BeFalse();
    }

    [Fact]
    public void IsOwner_UnprefixedId_ReturnsTrue_ForAnyEmail()
    {
        // Demo / unnamespaced records are accessible to everyone.
        ClientIdHelper.IsOwner("any@example.com", "demo-client-001").Should().BeTrue();
    }

    [Fact]
    public void IsOwner_TamperedPrefix_ReturnsFalse()
    {
        var legitimate = ClientIdHelper.Namespace("alice@example.com", "patient-001");
        // Swap the first character of the prefix to simulate a forged ID.
        var tampered = (legitimate[0] == 'a' ? "b" : "a") + legitimate[1..];
        ClientIdHelper.IsOwner("alice@example.com", tampered).Should().BeFalse();
    }
}
