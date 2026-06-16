using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Theragraf.Core.Exceptions;
using Theragraf.Core.Models;
using Theragraf.Functions.Activities;
using Theragraf.Functions.Agents;

namespace Theragraf.Tests.Activities;

public class Icd10ActivityTests
{
    private readonly IIcd10Agent _icd10Agent;
    private readonly Icd10Activity _sut;

    private static readonly SoapNote Note = new("S", "O", "A", "P");
    private static readonly IcdCode Code1 = new("F82", "Developmental coordination disorder", "Fine motor delays documented.");
    private static readonly IcdCode Code2 = new("F81.81", "Disorder of written expression", "Letter reversals noted.");

    public Icd10ActivityTests()
    {
        _icd10Agent = Substitute.For<IIcd10Agent>();
        _sut = new Icd10Activity(_icd10Agent, NullLoggerFactory.Instance);
    }

    [Fact]
    public async Task Run_DelegatesToIcd10Agent()
    {
        var input = new Icd10ActivityInput(Note, TherapyDiscipline.OccupationalTherapy);
        _icd10Agent.SuggestIcdCodesAsync(Note, TherapyDiscipline.OccupationalTherapy, null)
            .Returns(new List<IcdCode> { Code1 });

        var result = await _sut.Run(input);

        result.Should().ContainSingle().Which.Should().Be(Code1);
    }

    [Fact]
    public async Task Run_PassesCorrectDisciplineToAgent()
    {
        var input = new Icd10ActivityInput(Note, TherapyDiscipline.PhysicalTherapy);
        _icd10Agent.SuggestIcdCodesAsync(Arg.Any<SoapNote>(), Arg.Any<TherapyDiscipline>(), Arg.Any<ClientDemographicsSummary?>())
            .Returns(new List<IcdCode>());

        await _sut.Run(input);

        await _icd10Agent.Received(1).SuggestIcdCodesAsync(Note, TherapyDiscipline.PhysicalTherapy, null);
    }

    [Fact]
    public async Task Run_ReturnsAllCodesFromAgent()
    {
        var input = new Icd10ActivityInput(Note, TherapyDiscipline.OccupationalTherapy);
        _icd10Agent.SuggestIcdCodesAsync(Arg.Any<SoapNote>(), Arg.Any<TherapyDiscipline>(), Arg.Any<ClientDemographicsSummary?>())
            .Returns(new List<IcdCode> { Code1, Code2 });

        var result = await _sut.Run(input);

        result.Should().HaveCount(2).And.Contain(Code1).And.Contain(Code2);
    }

    [Fact]
    public async Task Run_AgentThrows_ExceptionPropagates()
    {
        var input = new Icd10ActivityInput(Note, TherapyDiscipline.OccupationalTherapy);
        _icd10Agent.SuggestIcdCodesAsync(Arg.Any<SoapNote>(), Arg.Any<TherapyDiscipline>(), Arg.Any<ClientDemographicsSummary?>())
            .Returns<IReadOnlyList<IcdCode>>(_ => throw new InvalidOperationException("LLM unavailable"));

        var act = async () => await _sut.Run(input);

        await act.Should().ThrowAsync<AgentException>().WithMessage("LLM unavailable");
    }

    [Fact]
    public async Task Run_ForwardsDemographicsToAgent()
    {
        var demographics = new ClientDemographicsSummary(AgeYears: 8, Sex: BiologicalSex.Male, PriorDiagnoses: "F82", FunctionalLimitations: null);
        var input = new Icd10ActivityInput(Note, TherapyDiscipline.OccupationalTherapy, demographics);
        _icd10Agent.SuggestIcdCodesAsync(Arg.Any<SoapNote>(), Arg.Any<TherapyDiscipline>(), Arg.Any<ClientDemographicsSummary?>())
            .Returns(new List<IcdCode>());

        await _sut.Run(input);

        await _icd10Agent.Received(1).SuggestIcdCodesAsync(Note, TherapyDiscipline.OccupationalTherapy, demographics);
    }
}
