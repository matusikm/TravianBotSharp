using System.Text.Json;

namespace MainCore.Shared;

public static class PageEvaluateExtensions
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    /// <summary>
    /// Runs a JS function in the page and returns its result as <typeparamref name="T"/>.
    /// The result travels as a JSON string and is deserialized with System.Text.Json instead of
    /// Playwright's EvaluateAsync&lt;T&gt; converter. That converter resolves object references by
    /// id and can hand an array of one DTO type to a property of another ("QueueBuildingDto[]
    /// cannot be converted to BuildingNameDto[]"), which broke the update-building task.
    /// </summary>
    public static async Task<T> EvaluateJsonAsync<T>(this IPage page, string function)
    {
        var json = await page.EvaluateAsync<string>($"async () => JSON.stringify(await ({function})())");
        return JsonSerializer.Deserialize<T>(json, Options)!;
    }
}
