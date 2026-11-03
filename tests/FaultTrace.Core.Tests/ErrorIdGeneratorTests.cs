using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using FaultTrace.Core.ErrorIds;
using Shouldly;
using Xunit;

namespace FaultTrace.Core.Tests;

public class ErrorIdGeneratorTests
{
    private const int SampleSize = 20_000;

    [Fact]
    public void NewId_HasExpectedFormat()
    {
        for (var i = 0; i < SampleSize; i++)
        {
            var id = ErrorIdGenerator.NewId();

            Regex.IsMatch(id, "^[A-Z2-9]{4}-[A-Z2-9]{4}$", RegexOptions.None, TimeSpan.FromSeconds(1))
                .ShouldBeTrue(id);
        }
    }

    [Fact]
    public void NewId_NeverContainsAmbiguousCharacters()
    {
        var used = Sample().SelectMany(id => id).ToHashSet();

        foreach (var ambiguous in "0O1IL")
        {
            used.ShouldNotContain(ambiguous);
        }
    }

    [Fact]
    public void NewId_UsesTheWholeAlphabet()
    {
        var used = Sample().SelectMany(id => id).Where(c => c != '-').ToHashSet();

        used.Count.ShouldBe(31);
    }

    [Fact]
    public void NewId_DoesNotRepeat()
    {
        // Larger samples would repeat by chance: there are 31^8 possible IDs, so 20,000 of them collide about once in 4,000 runs.
        var ids = Enumerable.Range(0, 1000).Select(_ => ErrorIdGenerator.NewId()).ToList();

        ids.Distinct().Count().ShouldBe(ids.Count);
    }

    private static IEnumerable<string> Sample() =>
        Enumerable.Range(0, SampleSize).Select(_ => ErrorIdGenerator.NewId());
}
