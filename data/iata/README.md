# IATA Airport Dataset

## Source

- **File:** `airports.dat`
- **Source URL:** https://raw.githubusercontent.com/jpatokal/openflights/master/data/airports.dat
- **Project:** [OpenFlights](https://openflights.org/data.html)
- **Snapshot date:** 2026-04-16
- **Row count:** 7,698 airports

## Licence

OpenFlights data is licensed under **Creative Commons Attribution-ShareAlike 3.0** (CC-BY-SA 3.0).

See https://creativecommons.org/licenses/by-sa/3.0/ for the full licence text.

**Required attribution** (per CC-BY-SA 3.0):

> Airport data by OpenFlights (https://openflights.org), licensed under CC-BY-SA 3.0.

This attribution is surfaced in the B2C portal's airport autocomplete footer text ("Airport data OpenFlights CC-BY-SA") and in the public `/airports?q=…` endpoint's response `X-Data-Attribution` header.

Any redistribution or derivative dataset generated from this file MUST preserve the same CC-BY-SA 3.0 licence.

## Schema

The file is a comma-separated-values dump with NO header row. Columns (per the OpenFlights documentation):

| # | Field         | Description                                                      |
|---|---------------|------------------------------------------------------------------|
| 1 | Airport ID    | Unique OpenFlights identifier                                    |
| 2 | Name          | Full airport name, may contain diacritics                        |
| 3 | City          | Main city served                                                 |
| 4 | Country       | Country (or territory) name                                      |
| 5 | IATA          | 3-letter IATA code, or `\N` when unassigned                      |
| 6 | ICAO          | 4-letter ICAO code, or `\N` when unassigned                      |
| 7 | Latitude      | Decimal degrees                                                  |
| 8 | Longitude     | Decimal degrees                                                  |
| 9 | Altitude      | Feet                                                             |
| 10| Timezone      | Hours offset from UTC                                            |
| 11| DST           | Daylight-saving rule code (E/A/S/O/Z/N/U)                        |
| 12| Tz database   | Tz database time zone (e.g. `Europe/London`)                     |
| 13| Type          | `airport` / `station` / `port` / `unknown`                       |
| 14| Source        | Provenance (`OurAirports` / `Legacy` / `User`)                   |

## Parsing rules (applied by `IataAirportSeeder`)

1. Skip rows where column 5 (IATA) is `\N`, empty, or not exactly 3 uppercase ASCII letters.
2. Skip rows where column 13 (Type) is not `airport` — stations, ports, and unknowns are excluded.
3. Decode embedded double-quotes with the RFC 4180 convention (`""` → `"`).
4. Normalise strings with `string.Trim` and lower-case the prefix index.

## Refresh

Re-download with:

```bash
curl -sL "https://raw.githubusercontent.com/jpatokal/openflights/master/data/airports.dat" \
  -o data/iata/airports.dat
```

The seeder sets a `iata:seed:done` Redis flag on completion. To force a reseed after a file refresh, set `FORCE_RESEED=true` on SearchService startup.

## Deferred

- IATA official subscription dataset (paid). Deferred to v2 — OpenFlights is sufficient for launch.
- Multilingual airport names. Deferred — English names only.
