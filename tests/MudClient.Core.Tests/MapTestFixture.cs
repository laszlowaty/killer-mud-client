namespace MudClient.Core.Tests;

internal static class MapTestFixture
{
    public const string SampleJson = """
    {
      "anonymousAreaName": "Unnamed Area",
      "areaCount": 2,
      "areas": [
        {
          "id": 1,
          "name": "Stary Kontynent",
          "roomCount": 6,
          "labels": [
            { "id": 0, "coordinates": [1, 2, 0], "image": ["abc123"] }
          ],
          "rooms": [
            {
              "id": 1,
              "name": "Las",
              "coordinates": [0, 0, 0],
              "environment": 119,
              "exits": [ { "exitId": 2, "name": "east" } ],
              "userData": { "sector": "las", "vnum": "1001" },
              "weight": 3
            },
            {
              "id": 2,
              "name": "Ścieżka",
              "coordinates": [1, 0, 0],
              "environment": 3,
              "exits": [ { "exitId": 1, "name": "west" } ],
              "userData": { "sector": "sciezka", "vnum": 1002 }
            },
            {
              "id": 3,
              "coordinates": [-5, -5, -1],
              "exits": [],
              "extraField": "should be ignored"
            },
            {
              "id": 4,
              "name": "Kolizja A",
              "coordinates": [10, 10, 0],
              "exits": [ { "exitId": 5, "name": "east" } ],
              "userData": { "vnum": "2000" }
            },
            {
              "id": 5,
              "name": "Kolizja B",
              "coordinates": [10, 10, 0],
              "exits": [ { "exitId": 4, "name": "west" } ],
              "userData": { "vnum": "2000" }
            },
            {
              "id": 6,
              "name": "Zła geometria",
              "coordinates": [1, 2],
              "exits": []
            }
          ]
        },
        {
          "id": 2,
          "name": "Nowy Kontynent",
          "roomCount": 1,
          "rooms": [
            {
              "id": 101,
              "name": "Brama",
              "coordinates": [0, 0, 1],
              "exits": [ { "exitId": 999, "name": "unknown-direction", "door": "closed" } ],
              "userData": { "sector": "Arktyczny Ląd" }
            }
          ]
        }
      ]
    }
    """;
}
