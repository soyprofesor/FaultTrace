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
        // Small on purpose: 20,000 IDs out of 31^8 repeat by pure chance about once in 4,000 runs, which would make this flaky.
        var ids = Enumerable.Range(0, 1000).Select(_ => ErrorIdGenerator.NewId()).ToList();

        ids.Distinct().Count().ShouldBe(ids.Count);
    }

    private static IEnumerable<string> Sample() =>
        Enumerable.Range(0, SampleSize).Select(_ => ErrorIdGenerator.NewId());
}
