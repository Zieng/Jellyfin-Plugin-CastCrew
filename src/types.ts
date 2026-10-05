export interface MovieCredit {
  id: string;
  title: string;
  role: string;
  year: number;
  posterUrl: string;
  type: 'Movie' | 'Series';
}

export interface PersonItem {
  Id: string;
  Name: string;
  Type: 'Person';
  Role: 'Actor' | 'Director' | 'Producer' | string;
  Overview: string;
  ProductionYear?: number;
  BirthDate?: string;
  BirthPlace?: string;
  DeathDate?: string;
  PrimaryImageTag?: string;
  ImageUrl: string;
  ProductionLocations: string[];
  Tags: string[];
  IsFavorite?: boolean;
  Libraries: string[];
  DateCreated: string;
  MovieCredits: MovieCredit[];
  Gender?: string;
  Age?: number;
  HeightCm?: number;
  BustCm?: number;
  WaistCm?: number;
  HipCm?: number;
  VideoCount?: number;
}

export interface CastCrewLibraryOption {
  Id: string;
  Name: string;
  CollectionType: string;
  ItemCount?: number;
}

export interface CastCrewActorsResponse {
  Items: PersonItem[];
  TotalRecordCount: number;
  StartIndex: number;
  PageSize: number;
  SortBy: string;
  SortOrder?: string;
  DetailRoutePreference: string;
  AvailableTags: string[];
  AvailableProductionLocations: string[];
  NameMatchItems?: PersonItem[] | null;
  NameMatchCount?: number | null;
  DescriptionMatchItems?: PersonItem[] | null;
  DescriptionMatchCount?: number | null;
  AvailableLibraries: CastCrewLibraryOption[];
  LibraryMappingLastSyncedUtc: string | null;
}

export interface PluginConfiguration {
  DefaultPageSize: number;
  DefaultSortBy: string;
  EnableCastCrewMainMenuEntry: boolean;
  EnableDebugLogging: boolean;
  DetailRoutePreference: string;
  IncludedLibraryIds: string[];
  // CastCrew 2.0 Metadata Provider Settings
  EnableTmdbProvider: boolean;
  TmdbApiToken: string;
  DefaultSelectTmdb: boolean;
  EnableMasexProvider: boolean;
  MasexActorUrl: string;
  DefaultSelectMasex: boolean;
  EnableNetflavProvider: boolean;
  NetflavActorUrl: string;
  DefaultSelectNetflav: boolean;
  UiLanguage: string;
}

export interface CastCrewActorsQuery {
  startIndex?: number;
  limit?: number;
  searchTerm?: string;
  sortBy?: string;
  sortOrder?: string;
  isFavorite?: boolean;
  tag?: string;
  productionLocation?: string;
  libraryIds?: string;
  userId?: string;
  // Local actor advanced filters
  minAge?: number;
  maxAge?: number;
  minHeight?: number;
  maxHeight?: number;
  minBust?: number;
  maxBust?: number;
  minWaist?: number;
  maxWaist?: number;
  minHip?: number;
  maxHip?: number;
  minVideoCount?: number;
  maxVideoCount?: number;
}

export interface LogEntry {
  id: string;
  timestamp: string;
  level: 'Information' | 'Debug' | 'Warning' | 'Error';
  message: string;
}

// 2.0 Metadata Models
export type MetadataPersonType = 'Actor' | 'Director' | 'Studio';

export interface MetadataPerson {
  Id: string;
  Source: string;
  Name: string;
  ProfileUrl?: string | null;
  ImageUrl?: string | null;
  Gender?: string | null;
  Age?: number | null;
  HeightCm?: number | null;
  BustCm?: number | null;
  WaistCm?: number | null;
  HipCm?: number | null;
  VideoCount?: number | null;
  Country?: string | null;
  Overview?: string | null;
  ExternalId?: string | null;
}

export interface MetadataSearchQuery {
  type: MetadataPersonType;
  name?: string;
  sources?: string;
  minAge?: number;
  maxAge?: number;
  minHeight?: number;
  maxHeight?: number;
  minBust?: number;
  maxBust?: number;
  minWaist?: number;
  maxWaist?: number;
  minHip?: number;
  maxHip?: number;
  minVideoCount?: number;
  maxVideoCount?: number;
  startIndex?: number;
  limit?: number;
}

export interface MetadataSearchResponse {
  Items: MetadataPerson[];
  TotalRecordCount: number;
  EnabledSources: string[];
  Errors: string[];
}
