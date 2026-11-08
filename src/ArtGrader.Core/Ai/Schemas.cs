using System.Text.Json.Nodes;

namespace ArtGrader.Core.Ai;

internal static class Schemas
{
    public static JsonObject Rubric() =>
        Obj(
            new JsonObject
            {
                ["categories"] = Arr(
                    Obj(
                        new JsonObject
                        {
                            ["name"] = Str(),
                            ["criteria"] = Arr(
                                Obj(
                                    new JsonObject
                                    {
                                        ["text"] = Str(),
                                        ["assessedBy"] = Enum("ai", "teacher"),
                                    })),
                        })),
            });

    public static JsonObject Grading() =>
        Obj(
            new JsonObject
            {
                ["beschreibung"] = Str("Kurze sachliche Beschreibung dessen, was auf den Fotos zu sehen ist (2-4 Sätze)."),
                ["criteria"] = Arr(
                    Obj(
                        new JsonObject
                        {
                            ["criterionId"] = Str(),
                            ["level"] = Enum("uebertroffen", "erfuellt", "teilweise", "nicht"),
                            ["justification"] = Str(),
                            ["confidence"] = Enum("hoch", "mittel", "tief"),
                        })),
                ["overallComment"] = Str("Gesamtrückmeldung an die Schülerin oder den Schüler, 2-4 Sätze."),
                ["strengths"] = Arr(Str()),
                ["nextSteps"] = Arr(Str()),
            });

    private static JsonObject Obj(JsonObject properties)
    {
        var required = new JsonArray();
        foreach (var property in properties)
        {
            required.Add(property.Key);
        }
        return new JsonObject
        {
            ["type"] = "object",
            ["properties"] = properties,
            ["required"] = required,
            ["additionalProperties"] = false,
        };
    }

    private static JsonObject Arr(JsonObject items) => new() { ["type"] = "array", ["items"] = items };

    private static JsonObject Str(string? description = null)
    {
        var node = new JsonObject { ["type"] = "string" };
        if (description is not null)
        {
            node["description"] = description;
        }
        return node;
    }

    private static JsonObject Enum(params string[] values)
    {
        var array = new JsonArray();
        foreach (var value in values)
        {
            array.Add(value);
        }
        return new JsonObject { ["type"] = "string", ["enum"] = array };
    }
}
