import express, { Request, Response } from 'express';
import cors from 'cors';
import path from 'path';
import { fileURLToPath } from 'url';
import {
  MOCK_PERSONS,
  AVAILABLE_LIBRARIES,
  INITIAL_CONFIG,
  MOCK_EXTERNAL_METADATA,
} from './src/data/mockData';
import {
  PersonItem,
  LogEntry,
  PluginConfiguration,
  CastCrewActorsResponse,
  MetadataPerson,
  MetadataSearchResponse,
  MetadataPersonType,
} from './src/types';
import { toCountryCode } from './src/utils/formatters';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

const app = express();
const PORT = 3000;

app.use(cors());
app.use(express.json());

// Prevent browser from caching API responses
app.use((_req, res, next) => {
  res.set('Cache-Control', 'no-store, no-cache, must-revalidate, proxy-revalidate');
  res.set('Pragma', 'no-cache');
  res.set('Expires', '0');
  next();
});

// In-memory state
let currentConfig: PluginConfiguration = { ...INITIAL_CONFIG };
let libraryMappingLastSyncedUtc: string = new Date().toISOString();
const debugLogs: LogEntry[] = [
  {
    id: 'log-1',
    timestamp: new Date().toISOString(),
    level: 'Information',
    message: '[CastCrew] CastCrew 2.0 Plugin initialized with external metadata providers.',
  },
  {
    id: 'log-2',
    timestamp: new Date().toISOString(),
    level: 'Information',
    message: '[CastCrew] Enabled sources: TMDB, MASex, Netflav.',
  },
];

function addLog(level: LogEntry['level'], message: string) {
  const log: LogEntry = {
    id: 'log-' + Date.now() + '-' + Math.random().toString(36).substring(2, 7),
    timestamp: new Date().toISOString(),
    level,
    message,
  };
  debugLogs.unshift(log);
  if (debugLogs.length > 200) {
    debugLogs.pop();
  }
}

// 1. Manifest
app.get('/CastCrew/Manifest', (_req: Request, res: Response) => {
  addLog('Information', 'Serving CastCrew 2.0 manifest metadata. Version=2.0.0.0.');
  res.json([
    {
      guid: 'a1c3e5f7-2b4d-6e8f-0a1c-3e5f7b9d1e3a',
      name: 'CastCrew',
      description: 'Adds Cast & Crew browse and 2.0 multi-source external metadata provider search to Jellyfin.',
      overview: 'CastCrew 2.0 with TMDB, MASex, and Netflav integration and advanced actor filtering.',
      owner: 'CastCrew',
      category: 'General',
      versions: [
        {
          version: '2.0.0.0',
          changelog: 'CastCrew 2.0: Added multi-source external metadata provider search (TMDB, MASex, Netflav) and advanced actor attribute filtering.',
          targetAbi: '10.10.7.0',
          sourceUrl: '',
          checksum: '',
          timestamp: '2026-10-05T00:00:00Z',
        },
      ],
    },
  ]);
});

// 2. Available Libraries
app.get('/CastCrew/Libraries', (_req: Request, res: Response) => {
  addLog('Information', `Listing available libraries for configuration. Count=${AVAILABLE_LIBRARIES.length}.`);
  res.json(AVAILABLE_LIBRARIES);
});

// 3. Refresh Library Mapping
app.post('/CastCrew/Libraries/RefreshMapping', (req: Request, res: Response) => {
  const reason = (req.query.reason as string) || 'manual/api';
  libraryMappingLastSyncedUtc = new Date().toISOString();

  if (currentConfig.EnableDebugLogging) {
    addLog(
      'Debug',
      `[CastCrew][Debug] Trigger person-to-library mapping (reason: ${reason}). Included libraries: ${
        currentConfig.IncludedLibraryIds.length ? currentConfig.IncludedLibraryIds.join(', ') : '(all)'
      }`
    );
  }

  addLog('Information', `Person-to-library mapping refresh finished. LastBuildTimeUtc=${libraryMappingLastSyncedUtc}.`);
  res.status(204).send();
});

// 4. Configuration endpoints (Extended for CastCrew 2.0)
app.get('/System/Configuration/CastCrew', (_req: Request, res: Response) => {
  res.json(currentConfig);
});

app.post('/System/Configuration/CastCrew', (req: Request, res: Response) => {
  const updated = req.body as Partial<PluginConfiguration>;
  currentConfig = {
    ...currentConfig,
    ...updated,
    DefaultPageSize: Math.max(10, Math.min(200, Number(updated.DefaultPageSize) || 50)),
  };

  if (currentConfig.EnableDebugLogging) {
    addLog('Debug', '[CastCrew][Debug] CastCrew 2.0 settings saved, updated provider configurations.');
  }
  libraryMappingLastSyncedUtc = new Date().toISOString();
  addLog('Information', 'CastCrew settings updated successfully.');

  res.json(currentConfig);
});

// 5. Query handler for Local Actors / Directors / Producers
function handlePeopleQuery(roleFilter: 'Actor' | 'Director' | 'Producer', req: Request, res: Response) {
  const startIndex = Math.max(0, parseInt(req.query.startIndex as string, 10) || 0);
  const limit = Math.max(1, Math.min(200, parseInt(req.query.limit as string, 10) || currentConfig.DefaultPageSize));
  const searchTerm = (req.query.searchTerm as string || '').trim().toLowerCase();
  const sortBy = (req.query.sortBy as string) || currentConfig.DefaultSortBy;
  const sortOrder = (req.query.sortOrder as string) || 'Ascending';
  const tagFilter = (req.query.tag as string) || '';
  const locationFilter = (req.query.productionLocation as string) || '';
  const libraryIdsParam = (req.query.libraryIds as string) || '';
  const isFavoriteFilter = req.query.isFavorite === 'true';

  if (currentConfig.EnableDebugLogging) {
    addLog(
      'Debug',
      `[CastCrew][Debug] ${roleFilter}s query accepted. StartIndex=${startIndex}, Limit=${limit}, SearchTerm=${searchTerm || '(none)'}, LibraryIds=${libraryIdsParam || '(all)'}.`
    );
  }

  // Determine active library scoping
  let activeLibraries: string[] = [];
  if (libraryIdsParam) {
    activeLibraries = libraryIdsParam.split(',').map((id) => id.trim()).filter(Boolean);
  } else if (currentConfig.IncludedLibraryIds.length > 0) {
    activeLibraries = currentConfig.IncludedLibraryIds;
  }

  // Base role filter
  let pool = MOCK_PERSONS.filter((p) => p.Role === roleFilter);

  // Library filter
  if (activeLibraries.length > 0) {
    pool = pool.filter((p) => p.Libraries.some((libId) => activeLibraries.includes(libId)));
  }

  // Tag filter
  if (tagFilter) {
    pool = pool.filter((p) => p.Tags.includes(tagFilter));
  }

  // Location filter
  if (locationFilter) {
    pool = pool.filter((p) => p.ProductionLocations.includes(locationFilter));
  }

  // Favorite filter
  if (isFavoriteFilter) {
    pool = pool.filter((p) => p.IsFavorite);
  }

  // 2.0 Local actor attribute filters (Age, Height, Measurements, Videos)
  const minAge = parseNum(req.query.minAge);
  const maxAge = parseNum(req.query.maxAge);
  const minHeight = parseNum(req.query.minHeight);
  const maxHeight = parseNum(req.query.maxHeight);
  const minBust = parseNum(req.query.minBust);
  const maxBust = parseNum(req.query.maxBust);
  const minWaist = parseNum(req.query.minWaist);
  const maxWaist = parseNum(req.query.maxWaist);
  const minHip = parseNum(req.query.minHip);
  const maxHip = parseNum(req.query.maxHip);
  const minVideoCount = parseNum(req.query.minVideoCount);
  const maxVideoCount = parseNum(req.query.maxVideoCount);

  if (minAge !== undefined || maxAge !== undefined) {
    pool = pool.filter((p) => inRange(p.Age, minAge, maxAge));
  }
  if (minHeight !== undefined || maxHeight !== undefined) {
    pool = pool.filter((p) => inRange(p.HeightCm, minHeight, maxHeight));
  }
  if (minBust !== undefined || maxBust !== undefined) {
    pool = pool.filter((p) => inRange(p.BustCm, minBust, maxBust));
  }
  if (minWaist !== undefined || maxWaist !== undefined) {
    pool = pool.filter((p) => inRange(p.WaistCm, minWaist, maxWaist));
  }
  if (minHip !== undefined || maxHip !== undefined) {
    pool = pool.filter((p) => inRange(p.HipCm, minHip, maxHip));
  }
  if (minVideoCount !== undefined || maxVideoCount !== undefined) {
    pool = pool.filter((p) => inRange(p.VideoCount, minVideoCount, maxVideoCount));
  }

  // Grouped search match logic (Name match vs Description match)
  let nameMatchItems: PersonItem[] | null = null;
  let nameMatchCount: number | null = null;
  let descMatchItems: PersonItem[] | null = null;
  let descMatchCount: number | null = null;

  if (searchTerm) {
    const nameMatches = pool.filter((p) => p.Name.toLowerCase().includes(searchTerm));
    const descMatches = pool.filter(
      (p) => !p.Name.toLowerCase().includes(searchTerm) && p.Overview.toLowerCase().includes(searchTerm)
    );

    nameMatchCount = nameMatches.length;
    descMatchCount = descMatches.length;
    nameMatchItems = nameMatches;
    descMatchItems = descMatches;

    pool = [...nameMatches, ...descMatches];
  }

  // Sorting
  pool = [...pool].sort((a, b) => {
    let cmp = 0;
    if (sortBy === 'DateCreated') {
      cmp = new Date(b.DateCreated).getTime() - new Date(a.DateCreated).getTime();
    } else if (sortBy === 'Random') {
      cmp = Math.random() - 0.5;
    } else {
      cmp = a.Name.localeCompare(b.Name);
    }
    return sortOrder === 'Descending' ? -cmp : cmp;
  });

  const totalRecordCount = pool.length;
  const pagedItems = pool.slice(startIndex, startIndex + limit);

  const allRolePersons = MOCK_PERSONS.filter((p) => p.Role === roleFilter);
  const availableTags = Array.from(new Set(allRolePersons.flatMap((p) => p.Tags)));
  const availableProductionLocations = Array.from(
    new Set(
      allRolePersons
        .flatMap((p) => (p.ProductionLocations || []).map(toCountryCode))
        .filter(Boolean)
    )
  );

  const response: CastCrewActorsResponse = {
    Items: pagedItems,
    TotalRecordCount: totalRecordCount,
    StartIndex: startIndex,
    PageSize: limit,
    SortBy: sortBy,
    SortOrder: sortOrder,
    DetailRoutePreference: currentConfig.DetailRoutePreference,
    AvailableTags: availableTags,
    AvailableProductionLocations: availableProductionLocations,
    NameMatchItems: searchTerm ? (nameMatchItems || []).slice(startIndex, startIndex + limit) : null,
    NameMatchCount: nameMatchCount,
    DescriptionMatchItems: searchTerm ? (descMatchItems || []).slice(startIndex, startIndex + limit) : null,
    DescriptionMatchCount: descMatchCount,
    AvailableLibraries: AVAILABLE_LIBRARIES,
    LibraryMappingLastSyncedUtc: libraryMappingLastSyncedUtc,
  };

  res.json(response);
}

app.get('/CastCrew/Actors', (req: Request, res: Response) => {
  handlePeopleQuery('Actor', req, res);
});

app.get('/CastCrew/Directors', (req: Request, res: Response) => {
  handlePeopleQuery('Director', req, res);
});

app.get('/CastCrew/Producers', (req: Request, res: Response) => {
  handlePeopleQuery('Producer', req, res);
});

// ============================================================================
// 6. CastCrew 2.0 External Metadata Provider Search: GET /CastCrew/Metadata/Search
// ============================================================================
function inRange(val: number | null | undefined, min?: number, max?: number): boolean {
  if (min === undefined && max === undefined) return true;
  if (val === null || val === undefined) return false;
  if (min !== undefined && val < min) return false;
  if (max !== undefined && val > max) return false;
  return true;
}

function parseNum(val: unknown): number | undefined {
  if (val === undefined || val === null || val === '') return undefined;
  const n = Number(val);
  return isNaN(n) ? undefined : n;
}

// Image proxy to bypass CDN referrer restrictions
app.get('/api/image-proxy', async (req: Request, res: Response) => {
  const imageUrl = req.query.url as string;
  if (!imageUrl) return res.status(400).send('Missing url');

  try {
    const upstreamRes = await fetch(imageUrl, {
      headers: {
        'User-Agent':
          'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36',
        Referer: 'https://netflav.com/',
      },
    });
    if (!upstreamRes.ok) {
      return res.redirect(imageUrl);
    }
    const contentType = upstreamRes.headers.get('content-type') || 'image/jpeg';
    res.setHeader('Content-Type', contentType);
    res.setHeader('Cache-Control', 'public, max-age=86400');
    const buffer = await upstreamRes.arrayBuffer();
    return res.send(Buffer.from(buffer));
  } catch {
    return res.redirect(imageUrl);
  }
});

// Live Netflav actress scraper
async function fetchNetflavLive(actressName: string): Promise<MetadataPerson | null> {
  try {
    const url = `https://netflav.com/all?actress=${encodeURIComponent(actressName)}`;
    const controller = new AbortController();
    const timeout = setTimeout(() => controller.abort(), 4000);
    const res = await fetch(url, {
      signal: controller.signal,
      headers: {
        'User-Agent':
          'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36',
        Accept: 'text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8',
      },
    });
    clearTimeout(timeout);
    if (!res.ok) return null;
    const html = await res.text();
    const match = html.match(/"actress"\s*:\s*(\{[^}]+\})/);
    if (match) {
      const data = JSON.parse(match[1]);
      if (data && data.name) {
        let age: number | undefined;
        if (data.birthday) {
          const birthYear = new Date(data.birthday).getFullYear();
          age = new Date().getFullYear() - birthYear;
        }
        const height = data.height ? parseInt(String(data.height).replace('cm', ''), 10) : undefined;
        const breast = data.breast ? parseInt(String(data.breast).replace('cm', ''), 10) : undefined;
        const waist = data.waist ? parseInt(String(data.waist).replace('cm', ''), 10) : undefined;
        const hip = data.hip ? parseInt(String(data.hip).replace('cm', ''), 10) : undefined;

        return {
          Id: `netflav-${encodeURIComponent(data.name.toLowerCase())}`,
          Source: 'Netflav',
          Name: data.name,
          ProfileUrl: url,
          ImageUrl: data.icon || 'https://pics.dmm.co.jp/mono/actjpgs/julia.jpg',
          Gender: 'Female',
          Age: age,
          HeightCm: isNaN(height as number) ? undefined : height,
          BustCm: isNaN(breast as number) ? undefined : breast,
          WaistCm: isNaN(waist as number) ? undefined : waist,
          HipCm: isNaN(hip as number) ? undefined : hip,
          VideoCount: data.videoCount || 0,
          Country: 'Japan',
          Overview: `Netflav 官方收录演员 ${data.name}，J罩杯 (胸:${breast || 102}/腰:${waist || 55}/臀:${hip || 84})。收录作品 ${data.videoCount || 0} 部。`,
          ExternalId: data.uid || `netflav-${data.name}`,
        };
      }
    }
  } catch {
    // Netflav live query timeout or offline, fallback to seeded data
  }
  return null;
}

app.get('/CastCrew/Metadata/Search', async (req: Request, res: Response) => {
  const type = ((req.query.type as string) || 'Actor') as MetadataPersonType;
  const nameQuery = (req.query.name as string || '').trim().toLowerCase();
  const minAge = parseNum(req.query.minAge);
  const maxAge = parseNum(req.query.maxAge);
  const minHeight = parseNum(req.query.minHeight);
  const maxHeight = parseNum(req.query.maxHeight);
  const minBust = parseNum(req.query.minBust);
  const maxBust = parseNum(req.query.maxBust);
  const minWaist = parseNum(req.query.minWaist);
  const maxWaist = parseNum(req.query.maxWaist);
  const minHip = parseNum(req.query.minHip);
  const maxHip = parseNum(req.query.maxHip);
  const minVideoCount = parseNum(req.query.minVideoCount);
  const maxVideoCount = parseNum(req.query.maxVideoCount);
  const startIndex = Math.max(0, parseInt(req.query.startIndex as string, 10) || 0);
  const limit = Math.max(1, Math.min(200, parseInt(req.query.limit as string, 10) || 50));

  // Determine enabled external sources from configuration
  const configEnabledSources: string[] = [];
  if (currentConfig.EnableTmdbProvider) configEnabledSources.push('TMDB');
  if (currentConfig.EnableMasexProvider) configEnabledSources.push('MASex');
  if (currentConfig.EnableNetflavProvider) configEnabledSources.push('Netflav');

  // Check if query specifically requested subset of sources (multi-select or single-select)
  const requestedSourcesParam = req.query.sources as string;
  let activeSources = [...configEnabledSources];
  if (requestedSourcesParam !== undefined) {
    const requested = requestedSourcesParam
      .split(',')
      .map((s) => s.trim())
      .filter(Boolean);
    activeSources = configEnabledSources.filter((s) => requested.includes(s));
  }

  if (currentConfig.EnableDebugLogging) {
    addLog(
      'Debug',
      `[CastCrew 2.0][Metadata] Search type=${type}, name=${nameQuery || '(none)'}, activeSources=[${activeSources.join(', ')}]`
    );
  }

  // Gather records only from selected and enabled providers
  const rawList: MetadataPerson[] = [];
  for (const src of activeSources) {
    const list = MOCK_EXTERNAL_METADATA[src as 'TMDB' | 'MASex' | 'Netflav'] || [];
    for (const item of list) {
      const itemCopy = { ...item };
      if (src === 'Netflav') {
        const netflavBase = (currentConfig.NetflavActorUrl || 'https://netflav.com/all?actress=').trim();
        itemCopy.ProfileUrl = netflavBase.includes('?')
          ? (netflavBase.endsWith('=')
              ? `${netflavBase}${encodeURIComponent(item.Name)}`
              : `${netflavBase}&actress=${encodeURIComponent(item.Name)}`)
          : `${netflavBase.replace(/\/+$/, '')}/all?actress=${encodeURIComponent(item.Name)}`;
      }
      if (type === 'Director') {
        if (item.Name === 'Christopher Nolan') rawList.push(itemCopy);
      } else if (type === 'Studio') {
        if (item.Name.includes('Pictures') || item.Name.includes('Studio')) rawList.push(itemCopy);
      } else {
        // Actor
        if (!item.Name.includes('Pictures') && item.Name !== 'Christopher Nolan') {
          rawList.push(itemCopy);
        }
      }
    }
  }

  // Live fetch from Netflav when Netflav is selected and a name query is supplied
  if (activeSources.includes('Netflav') && nameQuery) {
    const live = await fetchNetflavLive(nameQuery);
    if (live) {
      const idx = rawList.findIndex(
        (p) => p.Name.toLowerCase() === live.Name.toLowerCase() && p.Source.includes('Netflav')
      );
      if (idx >= 0) {
        rawList[idx] = live;
      } else {
        rawList.unshift(live);
      }
    }
  }

  // Deduplicate and merge by normalized name key (as in MetadataSearchService.cs)
  const groupedMap = new Map<string, MetadataPerson>();
  for (const item of rawList) {
    const key = item.Name.trim().replace(/\s+/g, '').toLowerCase();
    const existing = groupedMap.get(key);
    if (!existing) {
      groupedMap.set(key, { ...item });
    } else {
      // Merge properties
      if (!existing.ProfileUrl && item.ProfileUrl) existing.ProfileUrl = item.ProfileUrl;
      if (!existing.ImageUrl && item.ImageUrl) existing.ImageUrl = item.ImageUrl;
      if (!existing.Gender && item.Gender) existing.Gender = item.Gender;
      if (existing.Age == null && item.Age != null) existing.Age = item.Age;
      if (existing.HeightCm == null && item.HeightCm != null) existing.HeightCm = item.HeightCm;
      if (existing.BustCm == null && item.BustCm != null) existing.BustCm = item.BustCm;
      if (existing.WaistCm == null && item.WaistCm != null) existing.WaistCm = item.WaistCm;
      if (existing.HipCm == null && item.HipCm != null) existing.HipCm = item.HipCm;
      if (existing.VideoCount == null && item.VideoCount != null) existing.VideoCount = item.VideoCount;
      if (!existing.Country && item.Country) existing.Country = item.Country;
      if (!existing.Overview && item.Overview) existing.Overview = item.Overview;
      if (!existing.Source.includes(item.Source)) existing.Source += ',' + item.Source;
    }
  }

  // Apply search query and range filters
  let filtered = Array.from(groupedMap.values()).filter((p) => {
    // Name filter
    if (nameQuery && !p.Name.toLowerCase().includes(nameQuery)) {
      return false;
    }

    // Actor measurement and activity filters (Studio bypasses measurements)
    if (type !== 'Studio') {
      if (!inRange(p.Age, minAge, maxAge)) return false;
      if (!inRange(p.HeightCm, minHeight, maxHeight)) return false;
      if (!inRange(p.BustCm, minBust, maxBust)) return false;
      if (!inRange(p.WaistCm, minWaist, maxWaist)) return false;
      if (!inRange(p.HipCm, minHip, maxHip)) return false;
    }

    if (!inRange(p.VideoCount, minVideoCount, maxVideoCount)) return false;

    return true;
  });

  // Sort alphabetically by name
  filtered.sort((a, b) => a.Name.localeCompare(b.Name));

  const totalRecordCount = filtered.length;
  const pagedItems = filtered.slice(startIndex, startIndex + limit);

  const response: MetadataSearchResponse = {
    Items: pagedItems,
    TotalRecordCount: totalRecordCount,
    EnabledSources: activeSources,
    Errors: [],
  };

  res.json(response);
});

// Single item detail
app.get('/Items/:id', (req: Request, res: Response) => {
  const person = MOCK_PERSONS.find((p) => p.Id === req.params.id);
  if (!person) {
    return res.status(404).json({ error: 'Item not found' });
  }
  res.json(person);
});

// Debug logs endpoint
app.get('/CastCrew/Logs', (_req: Request, res: Response) => {
  res.json(debugLogs);
});

// Favorite toggle helper
app.post('/Items/:id/Favorite', (req: Request, res: Response) => {
  const person = MOCK_PERSONS.find((p) => p.Id === req.params.id);
  if (person) {
    person.IsFavorite = !person.IsFavorite;
    addLog('Information', `Toggled favorite for person "${person.Name}" to ${person.IsFavorite}.`);
    return res.json({ id: person.Id, isFavorite: person.IsFavorite });
  }
  res.status(404).json({ error: 'Person not found' });
});

// CastCrew 2.0: Write external actor metadata (Age, Three sizes: Bust, Waist, Hip) into local actor detail
app.post('/CastCrew/Metadata/ApplyToLocal', (req: Request, res: Response) => {
  const { name, age, heightCm, bustCm, waistCm, hipCm, overview, imageUrl } = req.body;
  if (!name || typeof name !== 'string') {
    return res.status(400).json({ error: 'Name is required' });
  }

  const normalized = name.trim().toLowerCase();
  let person = MOCK_PERSONS.find((p) => {
    const pNorm = p.Name.trim().toLowerCase();
    return pNorm === normalized || pNorm.includes(normalized) || normalized.includes(pNorm);
  });

  if (person) {
    if (age !== undefined && age !== null && age !== '') person.Age = Number(age);
    if (heightCm !== undefined && heightCm !== null && heightCm !== '') person.HeightCm = Number(heightCm);
    if (bustCm !== undefined && bustCm !== null && bustCm !== '') person.BustCm = Number(bustCm);
    if (waistCm !== undefined && waistCm !== null && waistCm !== '') person.WaistCm = Number(waistCm);
    if (hipCm !== undefined && hipCm !== null && hipCm !== '') person.HipCm = Number(hipCm);
    if (overview && (!person.Overview || person.Overview.length < overview.length)) {
      person.Overview = overview;
    }
    if (imageUrl) {
      person.ImageUrl = imageUrl;
      person.PrimaryImageTag = 'tag-' + Date.now();
      if (person.MovieCredits && person.MovieCredits.length > 0) {
        person.MovieCredits[0].posterUrl = imageUrl;
      }
    }
    addLog(
      'Information',
      `[CastCrew 2.0] Updated local actor profile & photo for "${person.Name}" (Photo synced, Age=${person.Age}, Bust=${person.BustCm}, Waist=${person.WaistCm}, Hip=${person.HipCm}).`
    );
  } else {
    // Create new local actor record in library
    const newId = 'p-local-' + Date.now();
    person = {
      Id: newId,
      Name: name.trim(),
      Type: 'Person',
      Role: 'Actor',
      Gender: 'Female',
      Age: age !== undefined && age !== '' ? Number(age) : undefined,
      HeightCm: heightCm !== undefined && heightCm !== '' ? Number(heightCm) : undefined,
      BustCm: bustCm !== undefined && bustCm !== '' ? Number(bustCm) : undefined,
      WaistCm: waistCm !== undefined && waistCm !== '' ? Number(waistCm) : undefined,
      HipCm: hipCm !== undefined && hipCm !== '' ? Number(hipCm) : undefined,
      VideoCount: 1,
      Overview: overview || `Actor profile imported from CastCrew external metadata search for ${name.trim()}.`,
      ProductionLocations: ['International'],
      Tags: ['Metadata Synced'],
      Libraries: ['lib-movies'],
      DateCreated: new Date().toISOString(),
      ImageUrl:
        imageUrl ||
        'https://images.unsplash.com/photo-1534528741775-53994a69daeb?auto=format&fit=crop&w=400&h=500&q=80',
      MovieCredits: [
        {
          id: 'm-imp-' + Date.now(),
          title: 'Featured Collection Work',
          role: 'Actor',
          year: new Date().getFullYear(),
          type: 'Movie',
          posterUrl:
            imageUrl ||
            'https://images.unsplash.com/photo-1518709268805-4e9042af9f23?auto=format&fit=crop&w=300&h=450&q=80',
        },
      ],
    };
    MOCK_PERSONS.unshift(person);
    addLog(
      'Information',
      `[CastCrew 2.0] Added actor "${person.Name}" to local library with Age=${person.Age}, Measurements=${person.BustCm}/${person.WaistCm}/${person.HipCm}.`
    );
  }

  res.json({ success: true, person });
});

// Download endpoints for precompiled Jellyfin 10.10.x (.NET 8) plugin binaries
app.use('/download', express.static(path.join(__dirname, 'public', 'download')));

app.get('/api/download/zip', (_req: Request, res: Response) => {
  const file = path.join(__dirname, 'public', 'download', 'Jellyfin.Plugin.CastCrew-net8.0.zip');
  res.download(file, 'Jellyfin.Plugin.CastCrew-net8.0.zip');
});

app.get('/api/download/dll', (_req: Request, res: Response) => {
  const file = path.join(__dirname, 'public', 'download', 'Jellyfin.Plugin.CastCrew.dll');
  res.download(file, 'Jellyfin.Plugin.CastCrew.dll');
});

app.get('/api/download/pdb', (_req: Request, res: Response) => {
  const file = path.join(__dirname, 'public', 'download', 'Jellyfin.Plugin.CastCrew.pdb');
  res.download(file, 'Jellyfin.Plugin.CastCrew.pdb');
});

// 7. Mount Vite middleware or static dist
async function startServer() {
  if (process.env.NODE_ENV !== 'production') {
    const { createServer: createViteServer } = await import('vite');
    const vite = await createViteServer({
      server: { middlewareMode: true, host: '0.0.0.0', port: PORT },
      appType: 'spa',
    });
    app.use(vite.middlewares);
  } else {
    app.use(express.static(path.join(__dirname, 'dist')));
    app.get('*', (_req: Request, res: Response) => {
      res.sendFile(path.join(__dirname, 'dist', 'index.html'));
    });
  }

  app.listen(PORT, '0.0.0.0', () => {
    console.log(`[CastCrew 2.0] Server listening at http://0.0.0.0:${PORT}`);
  });
}

startServer().catch((err) => {
  console.error('[CastCrew] Failed to start server:', err);
  process.exit(1);
});
