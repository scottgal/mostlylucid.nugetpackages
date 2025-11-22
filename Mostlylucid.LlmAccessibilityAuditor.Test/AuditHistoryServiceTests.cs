using Microsoft.Extensions.Options;
using Mostlylucid.LlmAccessibilityAuditor.Models;
using Mostlylucid.LlmAccessibilityAuditor.Services;

namespace Mostlylucid.LlmAccessibilityAuditor.Test;

public class AuditHistoryServiceTests
{
    private readonly AccessibilityAuditorOptions _options;

    public AuditHistoryServiceTests()
    {
        _options = new AccessibilityAuditorOptions
        {
            StoreAuditHistory = true,
            MaxHistoryCount = 5
        };
    }

    private AuditHistoryService CreateService()
    {
        return new AuditHistoryService(Options.Create(_options));
    }

    [Fact]
    public void AddReport_StoresReport()
    {
        // Arrange
        var service = CreateService();
        var report = new AccessibilityAuditReport { PageUrl = "https://example.com" };

        // Act
        service.AddReport(report);

        // Assert
        var retrieved = service.GetReport(report.ReportId);
        Assert.NotNull(retrieved);
        Assert.Equal("https://example.com", retrieved.PageUrl);
    }

    [Fact]
    public void AddReport_WhenHistoryDisabled_DoesNotStore()
    {
        // Arrange
        _options.StoreAuditHistory = false;
        var service = CreateService();
        var report = new AccessibilityAuditReport { PageUrl = "https://example.com" };

        // Act
        service.AddReport(report);

        // Assert
        var retrieved = service.GetReport(report.ReportId);
        Assert.Null(retrieved);
    }

    [Fact]
    public void AddReport_ExceedsMaxCount_RemovesOldest()
    {
        // Arrange
        var service = CreateService();
        var reports = Enumerable.Range(1, 7)
            .Select(i => new AccessibilityAuditReport { PageUrl = $"https://example.com/{i}" })
            .ToList();

        // Act
        foreach (var report in reports)
        {
            service.AddReport(report);
        }

        // Assert
        var recent = service.GetRecentReports(10);
        Assert.Equal(5, recent.Count); // Max is 5
        Assert.Null(service.GetReport(reports[0].ReportId)); // First one should be removed
        Assert.Null(service.GetReport(reports[1].ReportId)); // Second one should be removed
        Assert.NotNull(service.GetReport(reports[6].ReportId)); // Last one should exist
    }

    [Fact]
    public void GetRecentReports_ReturnsInReverseOrder()
    {
        // Arrange
        var service = CreateService();
        var report1 = new AccessibilityAuditReport { PageUrl = "https://example.com/1" };
        var report2 = new AccessibilityAuditReport { PageUrl = "https://example.com/2" };

        service.AddReport(report1);
        service.AddReport(report2);

        // Act
        var recent = service.GetRecentReports(10);

        // Assert
        Assert.Equal(2, recent.Count);
        Assert.Equal("https://example.com/2", recent[0].PageUrl); // Most recent first
        Assert.Equal("https://example.com/1", recent[1].PageUrl);
    }

    [Fact]
    public void GetRecentReports_RespectsCount()
    {
        // Arrange
        var service = CreateService();
        for (int i = 0; i < 5; i++)
        {
            service.AddReport(new AccessibilityAuditReport { PageUrl = $"https://example.com/{i}" });
        }

        // Act
        var recent = service.GetRecentReports(2);

        // Assert
        Assert.Equal(2, recent.Count);
    }

    [Fact]
    public void GetReportsForUrl_FiltersCorrectly()
    {
        // Arrange
        var service = CreateService();
        service.AddReport(new AccessibilityAuditReport { PageUrl = "https://example.com/a" });
        service.AddReport(new AccessibilityAuditReport { PageUrl = "https://example.com/b" });
        service.AddReport(new AccessibilityAuditReport { PageUrl = "https://example.com/a" });

        // Act
        var reports = service.GetReportsForUrl("https://example.com/a");

        // Assert
        Assert.Equal(2, reports.Count);
        Assert.All(reports, r => Assert.Equal("https://example.com/a", r.PageUrl));
    }

    [Fact]
    public void Clear_RemovesAllReports()
    {
        // Arrange
        var service = CreateService();
        service.AddReport(new AccessibilityAuditReport { PageUrl = "https://example.com" });
        service.AddReport(new AccessibilityAuditReport { PageUrl = "https://example.com" });

        // Act
        service.Clear();

        // Assert
        var recent = service.GetRecentReports(10);
        Assert.Empty(recent);
    }

    [Fact]
    public void GetStatistics_CalculatesCorrectly()
    {
        // Arrange
        var service = CreateService();

        var report1 = new AccessibilityAuditReport
        {
            PageUrl = "https://example.com/1",
            OverallScore = 80,
            Issues = new List<AccessibilityIssue>
            {
                new() { Type = AccessibilityIssueType.MissingAltText, Severity = IssueSeverity.Critical },
                new() { Type = AccessibilityIssueType.MissingAriaLabel, Severity = IssueSeverity.Serious }
            }
        };

        var report2 = new AccessibilityAuditReport
        {
            PageUrl = "https://example.com/2",
            OverallScore = 90,
            Issues = new List<AccessibilityIssue>
            {
                new() { Type = AccessibilityIssueType.MissingAltText, Severity = IssueSeverity.Moderate }
            }
        };

        service.AddReport(report1);
        service.AddReport(report2);

        // Act
        var stats = service.GetStatistics();

        // Assert
        Assert.Equal(2, stats.TotalAudits);
        Assert.Equal(3, stats.TotalIssuesFound);
        Assert.Equal(1.5, stats.AverageIssuesPerPage);
        Assert.Equal(85, stats.AverageScore);
        Assert.Equal(1, stats.PagesWithCriticalIssues);
        Assert.Equal(2, stats.IssuesByType["MissingAltText"]);
        Assert.Equal(1, stats.IssuesBySeverity["Critical"]);
    }
}
