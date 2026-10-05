import React, { useState, useEffect, useCallback } from 'react';
import {
  Search,
  Users,
  Film,
  Clapperboard,
  Sparkles,
  ArrowUpDown,
  ChevronLeft,
  ChevronRight,
  FolderKanban,
  Heart,
  RotateCcw,
  Globe2,
  SlidersHorizontal,
} from 'lucide-react';
import { Header } from './components/Header';
import { Sidebar } from './components/Sidebar';
import { PersonCard } from './components/PersonCard';
import { PersonDetailModal } from './components/PersonDetailModal';
import { SettingsModal } from './components/SettingsModal';
import { DebugLogsModal } from './components/DebugLogsModal';
import { MetadataSearchTab } from './components/MetadataSearchTab';
import {
  PersonItem,
  CastCrewActorsResponse,
  PluginConfiguration,
  CastCrewLibraryOption,
  LogEntry,
} from './types';
import { INITIAL_CONFIG } from './data/mockData';

export const App: React.FC = () => {
  // Navigation & Role Tabs
  const [activeRole, setActiveRole] = useState<'Actor' | 'Director' | 'Producer'>('Actor');
  const [activeSection, setActiveSection] = useState<string>('cast_crew');

  // Search & Filtering State (Local Cast & Crew)
  const [searchTerm, setSearchTerm] = useState<string>('');
  const [sortBy, setSortBy] = useState<string>('Name');
  const [selectedLibrary, setSelectedLibrary] = useState<string>('');
  const [selectedTag, setSelectedTag] = useState<string>('');
  const [onlyFavorites, setOnlyFavorites] = useState<boolean>(false);

  // Local Measurements (三围) & Video Count (本地影片数) Filters
  const [showAdvancedLocalFilters, setShowAdvancedLocalFilters] = useState<boolean>(true);
  const [minBust, setMinBust] = useState<string>('');
  const [maxBust, setMaxBust] = useState<string>('');
  const [minWaist, setMinWaist] = useState<string>('');
  const [maxWaist, setMaxWaist] = useState<string>('');
  const [minHip, setMinHip] = useState<string>('');
  const [maxHip, setMaxHip] = useState<string>('');
  const [minVideoCount, setMinVideoCount] = useState<string>('');
  const [maxVideoCount, setMaxVideoCount] = useState<string>('');
  const [minAge, setMinAge] = useState<string>('');
  const [maxAge, setMaxAge] = useState<string>('');

  // Pagination State
  const [pageIndex, setPageIndex] = useState<number>(0);
  const [pageSize, setPageSize] = useState<number>(50);

  // Data State
  const [response, setResponse] = useState<CastCrewActorsResponse | null>(null);
  const [loading, setLoading] = useState<boolean>(true);
  const [error, setError] = useState<string | null>(null);
  const [libraries, setLibraries] = useState<CastCrewLibraryOption[]>([]);
  const [config, setConfig] = useState<PluginConfiguration>({ ...INITIAL_CONFIG });

  // Modal State
  const [selectedPerson, setSelectedPerson] = useState<PersonItem | null>(null);
  const [showSettings, setShowSettings] = useState<boolean>(false);
  const [showLogs, setShowLogs] = useState<boolean>(false);
  const [debugLogs, setDebugLogs] = useState<LogEntry[]>([]);
  const [isRefreshing, setIsRefreshing] = useState<boolean>(false);

  // Internationalization: default en, toggleable to zh
  const [locale, setLocale] = useState<'en' | 'zh'>('zh');

  // Fetch plugin configuration and library list on mount
  useEffect(() => {
    fetch('/System/Configuration/CastCrew')
      .then((res) => (res.ok ? res.json() : null))
      .then((data) => {
        if (data) {
          setConfig(data);
          setPageSize(data.DefaultPageSize || 50);
          setSortBy(data.DefaultSortBy || 'Name');
          if (data.UiLanguage === 'Chinese') {
            setLocale('zh');
          } else if (data.UiLanguage === 'English') {
            setLocale('en');
          }
        }
      })
      .catch((err) => console.error('Failed to load config:', err));

    fetch('/CastCrew/Libraries')
      .then((res) => (res.ok ? res.json() : []))
      .then((libs) => setLibraries(libs))
      .catch((err) => console.error('Failed to load libraries:', err));
  }, []);

  // Fetch people based on current tab, query params, and pagination
  const fetchPeople = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const endpoint =
        activeRole === 'Director'
          ? '/CastCrew/Directors'
          : activeRole === 'Producer'
          ? '/CastCrew/Producers'
          : '/CastCrew/Actors';

      const params = new URLSearchParams();
      params.set('startIndex', String(pageIndex * pageSize));
      params.set('limit', String(pageSize));
      params.set('_t', String(Date.now()));
      params.set('sortBy', sortBy);
      if (searchTerm) params.set('searchTerm', searchTerm);
      if (selectedLibrary) params.set('libraryIds', selectedLibrary);
      if (selectedTag) params.set('tag', selectedTag);
      if (onlyFavorites) params.set('isFavorite', 'true');

      // Local measurement (三围) & video count query params
      if (activeRole === 'Actor') {
        if (minBust) params.set('minBust', minBust);
        if (maxBust) params.set('maxBust', maxBust);
        if (minWaist) params.set('minWaist', minWaist);
        if (maxWaist) params.set('maxWaist', maxWaist);
        if (minHip) params.set('minHip', minHip);
        if (maxHip) params.set('maxHip', maxHip);
        if (minVideoCount) params.set('minVideoCount', minVideoCount);
        if (maxVideoCount) params.set('maxVideoCount', maxVideoCount);
        if (minAge) params.set('minAge', minAge);
        if (maxAge) params.set('maxAge', maxAge);
      }

      const res = await fetch(`${endpoint}?${params.toString()}`);
      if (!res.ok) {
        throw new Error(`Server returned HTTP ${res.status}`);
      }
      const data: CastCrewActorsResponse = await res.json();
      let mergedItems = data.Items;
      let mergedNameMatches = data.NameMatchItems;
      try {
        const existingRaw = localStorage.getItem('castcrew_synced_persons');
        if (existingRaw) {
          const map: Record<string, PersonItem> = JSON.parse(existingRaw);
          const applySync = (p: PersonItem) => {
            const key = p.Name.trim().toLowerCase();
            return map[key] ? { ...p, ...map[key] } : p;
          };
          mergedItems = mergedItems.map(applySync);
          if (mergedNameMatches) mergedNameMatches = mergedNameMatches.map(applySync);
        }
      } catch {}

      setResponse({ ...data, Items: mergedItems, NameMatchItems: mergedNameMatches });
    } catch (err: unknown) {
      const message = err instanceof Error ? err.message : 'Unknown error';
      setError(message);
    } finally {
      setLoading(false);
    }
  }, [
    activeRole,
    pageIndex,
    pageSize,
    sortBy,
    searchTerm,
    selectedLibrary,
    selectedTag,
    onlyFavorites,
    minBust,
    maxBust,
    minWaist,
    maxWaist,
    minHip,
    maxHip,
    minVideoCount,
    maxVideoCount,
    minAge,
    maxAge,
  ]);

  useEffect(() => {
    if (activeSection !== 'metadata_search') {
      fetchPeople();
    }
  }, [activeSection, fetchPeople]);

  // Handle manual rebuild of person-to-library mapping
  const handleRefreshMapping = async () => {
    setIsRefreshing(true);
    try {
      await fetch('/CastCrew/Libraries/RefreshMapping?reason=ui-header', { method: 'POST' });
      await fetchPeople();
    } catch (err) {
      console.error('Failed to refresh mapping:', err);
    } finally {
      setIsRefreshing(false);
    }
  };

  // Toggle favorite for a person
  const handleToggleFavorite = async (personId: string, e: React.MouseEvent) => {
    e.stopPropagation();
    try {
      const res = await fetch(`/Items/${personId}/Favorite`, { method: 'POST' });
      if (res.ok) {
        const result = await res.json();
        setResponse((prev) => {
          if (!prev) return prev;
          const updateItem = (item: PersonItem) =>
            item.Id === personId ? { ...item, IsFavorite: result.isFavorite } : item;

          return {
            ...prev,
            Items: prev.Items.map(updateItem),
            NameMatchItems: prev.NameMatchItems ? prev.NameMatchItems.map(updateItem) : null,
            DescriptionMatchItems: prev.DescriptionMatchItems
              ? prev.DescriptionMatchItems.map(updateItem)
              : null,
          };
        });
        if (selectedPerson && selectedPerson.Id === personId) {
          setSelectedPerson((prev) => (prev ? { ...prev, IsFavorite: result.isFavorite } : prev));
        }
      }
    } catch (err) {
      console.error('Failed to toggle favorite:', err);
    }
  };

  // Immediate reactive update when an actor's photo and measurements are synced from external metadata
  const handleActorUpdated = (updatedPerson: PersonItem) => {
    // 1. Immediately update selectedPerson if currently viewed in details modal
    setSelectedPerson((prev) => {
      if (!prev) return prev;
      if (
        prev.Id === updatedPerson.Id ||
        prev.Name.trim().toLowerCase() === updatedPerson.Name.trim().toLowerCase()
      ) {
        return { ...prev, ...updatedPerson };
      }
      return prev;
    });

    // 2. Immediately update response items in list state
    setResponse((prev) => {
      if (!prev) return prev;
      const updateItem = (p: PersonItem) =>
        p.Id === updatedPerson.Id ||
        p.Name.trim().toLowerCase() === updatedPerson.Name.trim().toLowerCase()
          ? { ...p, ...updatedPerson }
          : p;

      const exists = prev.Items.some(
        (p) =>
          p.Id === updatedPerson.Id ||
          p.Name.trim().toLowerCase() === updatedPerson.Name.trim().toLowerCase()
      );

      const newItems = exists
        ? prev.Items.map(updateItem)
        : [{ ...updatedPerson }, ...prev.Items];

      return {
        ...prev,
        Items: newItems,
        NameMatchItems: prev.NameMatchItems ? prev.NameMatchItems.map(updateItem) : null,
        DescriptionMatchItems: prev.DescriptionMatchItems
          ? prev.DescriptionMatchItems.map(updateItem)
          : null,
        TotalRecordCount: exists ? prev.TotalRecordCount : prev.TotalRecordCount + 1,
      };
    });

    // Save to localStorage for persistent preview across reloads
    try {
      const existingRaw = localStorage.getItem('castcrew_synced_persons');
      const map: Record<string, PersonItem> = existingRaw ? JSON.parse(existingRaw) : {};
      map[updatedPerson.Name.trim().toLowerCase()] = updatedPerson;
      localStorage.setItem('castcrew_synced_persons', JSON.stringify(map));
    } catch {}

    // 3. Re-fetch from backend to ensure consistent state
    fetchPeople();
  };

  // Save plugin configuration
  const handleSaveConfig = async (newConfig: PluginConfiguration) => {
    const res = await fetch('/System/Configuration/CastCrew', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(newConfig),
    });
    if (res.ok) {
      const saved = await res.json();
      setConfig(saved);
      setPageSize(saved.DefaultPageSize);
      setSortBy(saved.DefaultSortBy);
      if (saved.UiLanguage === 'Chinese') setLocale('zh');
      if (saved.UiLanguage === 'English') setLocale('en');
      await fetchPeople();
    }
  };

  // Fetch debug logs
  const fetchDebugLogs = async () => {
    try {
      const res = await fetch('/CastCrew/Logs');
      if (res.ok) {
        const logs = await res.json();
        setDebugLogs(logs);
      }
    } catch (err) {
      console.error('Failed to fetch logs:', err);
    }
  };

  const handleOpenLogs = async () => {
    await fetchDebugLogs();
    setShowLogs(true);
  };

  const handleResetFilters = () => {
    setSearchTerm('');
    setSelectedLibrary('');
    setSelectedTag('');
    setOnlyFavorites(false);
    setMinBust('');
    setMaxBust('');
    setMinWaist('');
    setMaxWaist('');
    setMinHip('');
    setMaxHip('');
    setMinVideoCount('');
    setMaxVideoCount('');
    setMinAge('');
    setMaxAge('');
    setPageIndex(0);
  };

  const totalRecords = response?.TotalRecordCount || 0;
  const totalPages = Math.ceil(totalRecords / pageSize) || 1;

  const roleDisplayNames = {
    Actor: locale === 'zh' ? '演员' : 'Actors',
    Director: locale === 'zh' ? '导演' : 'Directors',
    Producer: locale === 'zh' ? '制片人' : 'Producers',
  };

  return (
    <div className="min-h-screen bg-[#0b0e14] text-[#e6edf3] flex flex-col font-sans">
      {/* Top Header */}
      <Header
        config={config}
        lastSynced={response?.LibraryMappingLastSyncedUtc || null}
        isRefreshing={isRefreshing}
        onRefreshMapping={handleRefreshMapping}
        onOpenSettings={() => setShowSettings(true)}
        onOpenLogs={handleOpenLogs}
        locale={locale}
        onToggleLocale={() => setLocale((prev) => (prev === 'en' ? 'zh' : 'en'))}
      />

      <div className="flex-1 flex overflow-hidden">
        {/* Sidebar */}
        <Sidebar
          activeSection={activeSection}
          onSelectSection={(sec) => {
            setActiveSection(sec);
            if (sec === 'favorites') {
              setOnlyFavorites(true);
            } else if (sec === 'movies') {
              setSelectedLibrary('lib-movies');
            } else if (sec === 'tv') {
              setSelectedLibrary('lib-tv');
            } else {
              handleResetFilters();
            }
          }}
          showMainMenuEntry={config.EnableCastCrewMainMenuEntry}
          locale={locale}
        />

        {/* Main Content Area */}
        <main className="flex-1 overflow-y-auto px-4 sm:px-8 py-6 max-w-7xl mx-auto w-full">
          {/* Sub-navigation tabs: Cast & Crew vs Metadata Search */}
          <div className="flex items-center gap-2 mb-6 bg-[#161c26] p-1.5 rounded-xl border border-[#232b38] w-fit">
            <button
              onClick={() => setActiveSection('cast_crew')}
              className={`flex items-center gap-2 px-4 py-2 rounded-lg text-xs font-semibold transition-all cursor-pointer ${
                activeSection !== 'metadata_search'
                  ? 'bg-[#00a4dc] text-white shadow-md'
                  : 'text-[#8b949e] hover:text-white'
              }`}
            >
              <Users className="w-4 h-4" />
              <span>{locale === 'zh' ? '本地媒体库人员总览' : 'Local Cast & Crew'}</span>
            </button>

            <button
              onClick={() => setActiveSection('metadata_search')}
              className={`flex items-center gap-2 px-4 py-2 rounded-lg text-xs font-semibold transition-all cursor-pointer ${
                activeSection === 'metadata_search'
                  ? 'bg-gradient-to-r from-emerald-500 to-teal-500 text-white shadow-md'
                  : 'text-[#8b949e] hover:text-white'
              }`}
            >
              <Globe2 className="w-4 h-4" />
              <span>{locale === 'zh' ? '元数据搜索' : 'Metadata Search'}</span>
            </button>
          </div>

          {/* SECTION A: CastCrew 2.0 External Metadata Search Tab */}
          {activeSection === 'metadata_search' ? (
            <MetadataSearchTab
              config={config}
              locale={locale}
              onActorSyncedToLocal={handleActorUpdated}
              onViewLocalDetail={(p) => setSelectedPerson(p)}
            />
          ) : (
            /* SECTION B: Local Cast & Crew Browse & Filtering */
            <>
              {/* Top Banner / Role Tabs & Toolbar */}
              <div className="space-y-4 mb-6">
                {/* Role Switcher Tabs */}
                <div className="flex items-center justify-between flex-wrap gap-4 border-b border-[#232b38] pb-4">
                  <div className="flex items-center gap-2 bg-[#161c26] p-1 rounded-xl border border-[#232b38]">
                    {(['Actor', 'Director', 'Producer'] as const).map((role) => (
                      <button
                        key={role}
                        onClick={() => {
                          setActiveRole(role);
                          setPageIndex(0);
                        }}
                        className={`flex items-center gap-2 px-4 py-2 rounded-lg text-sm font-medium transition-all cursor-pointer ${
                          activeRole === role
                            ? 'bg-[#00a4dc] text-white shadow-md shadow-[#00a4dc]/20'
                            : 'text-[#8b949e] hover:text-white hover:bg-[#1c2432]'
                        }`}
                      >
                        {role === 'Actor' ? (
                          <Users className="w-4 h-4" />
                        ) : role === 'Director' ? (
                          <Clapperboard className="w-4 h-4" />
                        ) : (
                          <Film className="w-4 h-4" />
                        )}
                        <span>{roleDisplayNames[role]}</span>
                      </button>
                    ))}
                  </div>

                  {/* Status and record counter */}
                  <div className="text-xs text-[#8b949e] flex items-center gap-3">
                    <span className="bg-[#161c26] px-3 py-1.5 rounded-lg border border-[#232b38]">
                      {locale === 'zh' ? '总数: ' : 'Total: '}
                      <strong className="text-white font-mono">{totalRecords}</strong>
                    </span>
                    {selectedLibrary && (
                      <span className="bg-[#00a4dc]/10 text-[#00a4dc] px-2.5 py-1.5 rounded-lg border border-[#00a4dc]/30 text-xs">
                        {libraries.find((l) => l.Id === selectedLibrary)?.Name}
                      </span>
                    )}
                  </div>
                </div>

                {/* Filter and Search Bar */}
                <div className="grid grid-cols-1 md:grid-cols-12 gap-3 items-center">
                  <div className="md:col-span-5 relative">
                    <Search className="w-4 h-4 absolute left-3.5 top-1/2 -translate-y-1/2 text-[#8b949e]" />
                    <input
                      type="search"
                      value={searchTerm}
                      onChange={(e) => {
                        setSearchTerm(e.target.value);
                        setPageIndex(0);
                      }}
                      placeholder={
                        locale === 'zh'
                          ? `搜索${roleDisplayNames[activeRole]}名称或简介...`
                          : `Search ${roleDisplayNames[activeRole].toLowerCase()} by name or overview...`
                      }
                      className="w-full bg-[#161c26] border border-[#232b38] focus:border-[#00a4dc] text-white rounded-xl pl-10 pr-4 py-2.5 text-sm outline-none transition-colors"
                    />
                  </div>

                  <div className="md:col-span-3 flex items-center gap-2 bg-[#161c26] border border-[#232b38] rounded-xl px-3 py-1.5">
                    <ArrowUpDown className="w-4 h-4 text-[#8b949e] shrink-0" />
                    <select
                      value={sortBy}
                      onChange={(e) => {
                        setSortBy(e.target.value);
                        setPageIndex(0);
                      }}
                      className="w-full bg-transparent text-white text-xs outline-none cursor-pointer"
                    >
                      <option value="Name">{locale === 'zh' ? '按名称 (Name)' : 'Sort: Name (A-Z)'}</option>
                      <option value="DateCreated">
                        {locale === 'zh' ? '最近添加 (Recently Added)' : 'Sort: Recently Added'}
                      </option>
                      <option value="Random">{locale === 'zh' ? '随机排序 (Random)' : 'Sort: Random'}</option>
                    </select>
                  </div>

                  <div className="md:col-span-3 flex items-center gap-2 bg-[#161c26] border border-[#232b38] rounded-xl px-3 py-1.5">
                    <FolderKanban className="w-4 h-4 text-[#00a4dc] shrink-0" />
                    <select
                      value={selectedLibrary}
                      onChange={(e) => {
                        setSelectedLibrary(e.target.value);
                        setPageIndex(0);
                      }}
                      className="w-full bg-transparent text-white text-xs outline-none cursor-pointer"
                    >
                      <option value="">{locale === 'zh' ? '全部媒体库 (All Libraries)' : 'All Libraries'}</option>
                      {libraries.map((lib) => (
                        <option key={lib.Id} value={lib.Id}>
                          {lib.Name}
                        </option>
                      ))}
                    </select>
                  </div>

                  <div className="md:col-span-1 flex justify-end gap-1.5">
                    <button
                      onClick={() => {
                        setOnlyFavorites((prev) => !prev);
                        setPageIndex(0);
                      }}
                      title={locale === 'zh' ? '只看收藏' : 'Filter by favorites'}
                      className={`p-2.5 rounded-xl border transition-colors cursor-pointer flex-1 flex items-center justify-center ${
                        onlyFavorites
                          ? 'bg-red-500/20 text-red-400 border-red-500/40'
                          : 'bg-[#161c26] text-[#8b949e] hover:text-white border-[#232b38]'
                      }`}
                    >
                      <Heart className={`w-4 h-4 ${onlyFavorites ? 'fill-current' : ''}`} />
                    </button>

                    {activeRole === 'Actor' && (
                      <button
                        onClick={() => setShowAdvancedLocalFilters((prev) => !prev)}
                        title={locale === 'zh' ? '三围与本地影片数高级筛选' : 'Measurements & video count filter'}
                        className={`p-2.5 rounded-xl border transition-colors cursor-pointer flex-1 flex items-center justify-center ${
                          showAdvancedLocalFilters ||
                          minBust ||
                          maxBust ||
                          minWaist ||
                          maxWaist ||
                          minHip ||
                          maxHip ||
                          minVideoCount ||
                          maxVideoCount ||
                          minAge ||
                          maxAge
                            ? 'bg-emerald-500/20 text-emerald-400 border-emerald-500/40'
                            : 'bg-[#161c26] text-[#8b949e] hover:text-white border-[#232b38]'
                        }`}
                      >
                        <SlidersHorizontal className="w-4 h-4" />
                      </button>
                    )}
                  </div>
                </div>

                {/* Advanced Local Actor Filter: Measurements (三围范围) & Local Video Count (本地影片数) */}
                {activeRole === 'Actor' && showAdvancedLocalFilters && (
                  <div className="bg-[#161c26] border border-emerald-500/30 rounded-2xl p-4 space-y-3 animate-in fade-in duration-200">
                    <div className="flex items-center justify-between text-xs font-semibold text-emerald-400">
                      <div className="flex items-center gap-2">
                        <SlidersHorizontal className="w-3.5 h-3.5" />
                        <span>
                          {locale === 'zh'
                            ? '本地演员高级搜索：三围范围与本地影片数'
                            : 'Filter by Measurements (B/W/H) & Local Video Count'}
                        </span>
                      </div>
                      {(minBust ||
                        maxBust ||
                        minWaist ||
                        maxWaist ||
                        minHip ||
                        maxHip ||
                        minVideoCount ||
                        maxVideoCount ||
                        minAge ||
                        maxAge) && (
                        <button
                          onClick={() => {
                            setMinBust('');
                            setMaxBust('');
                            setMinWaist('');
                            setMaxWaist('');
                            setMinHip('');
                            setMaxHip('');
                            setMinVideoCount('');
                            setMaxVideoCount('');
                            setMinAge('');
                            setMaxAge('');
                          }}
                          className="text-[11px] text-red-400 hover:underline cursor-pointer flex items-center gap-1"
                        >
                          <RotateCcw className="w-3 h-3" />
                          <span>{locale === 'zh' ? '清空三围与影片筛选' : 'Clear filters'}</span>
                        </button>
                      )}
                    </div>

                    <div className="grid grid-cols-2 sm:grid-cols-4 md:grid-cols-5 gap-3 pt-1">
                      {/* Bust range */}
                      <div>
                        <label className="block text-[11px] text-[#8b949e] mb-1">
                          {locale === 'zh' ? '胸围范围 (cm)' : 'Bust (cm)'}
                        </label>
                        <div className="flex items-center gap-1">
                          <input
                            type="number"
                            placeholder="Min"
                            value={minBust}
                            onChange={(e) => {
                              setMinBust(e.target.value);
                              setPageIndex(0);
                            }}
                            className="w-full bg-[#0f141c] border border-[#232b38] focus:border-[#00a4dc] text-white rounded-lg px-2.5 py-1.5 text-xs outline-none"
                          />
                          <span className="text-[#8b949e]">-</span>
                          <input
                            type="number"
                            placeholder="Max"
                            value={maxBust}
                            onChange={(e) => {
                              setMaxBust(e.target.value);
                              setPageIndex(0);
                            }}
                            className="w-full bg-[#0f141c] border border-[#232b38] focus:border-[#00a4dc] text-white rounded-lg px-2.5 py-1.5 text-xs outline-none"
                          />
                        </div>
                      </div>

                      {/* Waist range */}
                      <div>
                        <label className="block text-[11px] text-[#8b949e] mb-1">
                          {locale === 'zh' ? '腰围范围 (cm)' : 'Waist (cm)'}
                        </label>
                        <div className="flex items-center gap-1">
                          <input
                            type="number"
                            placeholder="Min"
                            value={minWaist}
                            onChange={(e) => {
                              setMinWaist(e.target.value);
                              setPageIndex(0);
                            }}
                            className="w-full bg-[#0f141c] border border-[#232b38] focus:border-[#00a4dc] text-white rounded-lg px-2.5 py-1.5 text-xs outline-none"
                          />
                          <span className="text-[#8b949e]">-</span>
                          <input
                            type="number"
                            placeholder="Max"
                            value={maxWaist}
                            onChange={(e) => {
                              setMaxWaist(e.target.value);
                              setPageIndex(0);
                            }}
                            className="w-full bg-[#0f141c] border border-[#232b38] focus:border-[#00a4dc] text-white rounded-lg px-2.5 py-1.5 text-xs outline-none"
                          />
                        </div>
                      </div>

                      {/* Hip range */}
                      <div>
                        <label className="block text-[11px] text-[#8b949e] mb-1">
                          {locale === 'zh' ? '臀围范围 (cm)' : 'Hip (cm)'}
                        </label>
                        <div className="flex items-center gap-1">
                          <input
                            type="number"
                            placeholder="Min"
                            value={minHip}
                            onChange={(e) => {
                              setMinHip(e.target.value);
                              setPageIndex(0);
                            }}
                            className="w-full bg-[#0f141c] border border-[#232b38] focus:border-[#00a4dc] text-white rounded-lg px-2.5 py-1.5 text-xs outline-none"
                          />
                          <span className="text-[#8b949e]">-</span>
                          <input
                            type="number"
                            placeholder="Max"
                            value={maxHip}
                            onChange={(e) => {
                              setMaxHip(e.target.value);
                              setPageIndex(0);
                            }}
                            className="w-full bg-[#0f141c] border border-[#232b38] focus:border-[#00a4dc] text-white rounded-lg px-2.5 py-1.5 text-xs outline-none"
                          />
                        </div>
                      </div>

                      {/* Local video count range */}
                      <div>
                        <label className="block text-[11px] text-[#8b949e] mb-1">
                          {locale === 'zh' ? '本地影片数量' : 'Local Videos'}
                        </label>
                        <div className="flex items-center gap-1">
                          <input
                            type="number"
                            placeholder="Min"
                            value={minVideoCount}
                            onChange={(e) => {
                              setMinVideoCount(e.target.value);
                              setPageIndex(0);
                            }}
                            className="w-full bg-[#0f141c] border border-[#232b38] focus:border-[#00a4dc] text-white rounded-lg px-2.5 py-1.5 text-xs outline-none"
                          />
                          <span className="text-[#8b949e]">-</span>
                          <input
                            type="number"
                            placeholder="Max"
                            value={maxVideoCount}
                            onChange={(e) => {
                              setMaxVideoCount(e.target.value);
                              setPageIndex(0);
                            }}
                            className="w-full bg-[#0f141c] border border-[#232b38] focus:border-[#00a4dc] text-white rounded-lg px-2.5 py-1.5 text-xs outline-none"
                          />
                        </div>
                      </div>

                      {/* Age range */}
                      <div>
                        <label className="block text-[11px] text-[#8b949e] mb-1">
                          {locale === 'zh' ? '年龄范围' : 'Age Range'}
                        </label>
                        <div className="flex items-center gap-1">
                          <input
                            type="number"
                            placeholder="Min"
                            value={minAge}
                            onChange={(e) => {
                              setMinAge(e.target.value);
                              setPageIndex(0);
                            }}
                            className="w-full bg-[#0f141c] border border-[#232b38] focus:border-[#00a4dc] text-white rounded-lg px-2.5 py-1.5 text-xs outline-none"
                          />
                          <span className="text-[#8b949e]">-</span>
                          <input
                            type="number"
                            placeholder="Max"
                            value={maxAge}
                            onChange={(e) => {
                              setMaxAge(e.target.value);
                              setPageIndex(0);
                            }}
                            className="w-full bg-[#0f141c] border border-[#232b38] focus:border-[#00a4dc] text-white rounded-lg px-2.5 py-1.5 text-xs outline-none"
                          />
                        </div>
                      </div>
                    </div>
                  </div>
                )}

                {/* Quick tag chips */}
                {response?.AvailableTags && response.AvailableTags.length > 0 && (
                  <div className="flex items-center gap-2 overflow-x-auto pb-1 text-xs">
                    <span className="text-[#8b949e] shrink-0 flex items-center gap-1 font-medium">
                      <Sparkles className="w-3.5 h-3.5 text-[#00a4dc]" />
                      {locale === 'zh' ? '快速标签:' : 'Tags:'}
                    </span>
                    <button
                      onClick={() => {
                        setSelectedTag('');
                        setPageIndex(0);
                      }}
                      className={`px-2.5 py-1 rounded-full text-[11px] font-medium transition-colors cursor-pointer shrink-0 ${
                        !selectedTag
                          ? 'bg-[#00a4dc] text-white'
                          : 'bg-[#161c26] text-[#8b949e] hover:text-white border border-[#232b38]'
                      }`}
                    >
                      {locale === 'zh' ? '全部' : 'All'}
                    </button>
                    {response.AvailableTags.map((tag) => (
                      <button
                        key={tag}
                        onClick={() => {
                          setSelectedTag((prev) => (prev === tag ? '' : tag));
                          setPageIndex(0);
                        }}
                        className={`px-2.5 py-1 rounded-full text-[11px] font-medium transition-colors cursor-pointer shrink-0 ${
                          selectedTag === tag
                            ? 'bg-[#00a4dc] text-white'
                            : 'bg-[#161c26] text-[#8b949e] hover:text-white border border-[#232b38]'
                        }`}
                      >
                        {tag}
                      </button>
                    ))}
                  </div>
                )}
              </div>

              {/* Error Message */}
              {error && (
                <div className="p-4 mb-6 rounded-xl bg-red-500/10 border border-red-500/30 text-red-300 text-sm flex items-center justify-between">
                  <span>{locale === 'zh' ? `加载数据失败: ${error}` : `Failed to load data: ${error}`}</span>
                  <button
                    onClick={fetchPeople}
                    className="px-3 py-1 bg-red-500/20 hover:bg-red-500/30 rounded-lg text-xs font-semibold cursor-pointer"
                  >
                    {locale === 'zh' ? '重试' : 'Retry'}
                  </button>
                </div>
              )}

              {/* Loading Indicator */}
              {loading && (
                <div className="py-20 flex flex-col items-center justify-center text-center space-y-3">
                  <div className="w-10 h-10 border-3 border-[#00a4dc]/30 border-t-[#00a4dc] rounded-full animate-spin"></div>
                  <p className="text-sm text-[#8b949e]">
                    {locale === 'zh' ? '正在加载演职人员信息...' : 'Loading Cast & Crew...'}
                  </p>
                </div>
              )}

              {/* Search Result Section: Grouped Matches */}
              {!loading && searchTerm && response && (
                <div className="space-y-8">
                  <div>
                    <div className="flex items-center justify-between pb-3 border-b border-[#232b38] mb-4">
                      <h2 className="text-base font-semibold text-white flex items-center gap-2">
                        <span>{locale === 'zh' ? '名称匹配' : 'Name matches'}</span>
                        <span className="text-xs px-2 py-0.5 rounded-full bg-[#00a4dc]/20 text-[#00a4dc] font-normal">
                          {response.NameMatchCount || 0} {locale === 'zh' ? '条' : 'found'}
                        </span>
                      </h2>
                    </div>

                    {response.NameMatchItems && response.NameMatchItems.length > 0 ? (
                      <div className="grid grid-cols-2 sm:grid-cols-3 md:grid-cols-4 lg:grid-cols-5 gap-4">
                        {response.NameMatchItems.map((person) => (
                          <PersonCard
                            key={person.Id}
                            person={person}
                            onSelect={setSelectedPerson}
                            onToggleFavorite={handleToggleFavorite}
                            locale={locale}
                          />
                        ))}
                      </div>
                    ) : (
                      <p className="text-xs text-[#8b949e] py-3">
                        {locale === 'zh' ? '未找到名称匹配的演职人员。' : 'No name matches found.'}
                      </p>
                    )}
                  </div>

                  <div>
                    <div className="flex items-center justify-between pb-3 border-b border-[#232b38] mb-4">
                      <h2 className="text-base font-semibold text-white flex items-center gap-2">
                        <span>{locale === 'zh' ? '简介匹配' : 'Description matches'}</span>
                        <span className="text-xs px-2 py-0.5 rounded-full bg-purple-500/20 text-purple-400 font-normal">
                          {response.DescriptionMatchCount || 0} {locale === 'zh' ? '条' : 'found'}
                        </span>
                      </h2>
                    </div>

                    {response.DescriptionMatchItems && response.DescriptionMatchItems.length > 0 ? (
                      <div className="grid grid-cols-2 sm:grid-cols-3 md:grid-cols-4 lg:grid-cols-5 gap-4">
                        {response.DescriptionMatchItems.map((person) => (
                          <PersonCard
                            key={person.Id}
                            person={person}
                            onSelect={setSelectedPerson}
                            onToggleFavorite={handleToggleFavorite}
                            locale={locale}
                          />
                        ))}
                      </div>
                    ) : (
                      <p className="text-xs text-[#8b949e] py-3">
                        {locale === 'zh' ? '未找到简介匹配的演职人员。' : 'No description matches found.'}
                      </p>
                    )}
                  </div>
                </div>
              )}

              {/* Standard Grid */}
              {!loading && !searchTerm && response && (
                <>
                  {response.Items.length === 0 ? (
                    <div className="py-20 text-center space-y-4 bg-[#161c26]/40 rounded-2xl border border-dashed border-[#232b38]">
                      <Users className="w-12 h-12 text-[#8b949e] mx-auto opacity-50" />
                      <div>
                        <h3 className="text-base font-semibold text-white">
                          {locale === 'zh' ? '未找到相关人员' : 'No persons found'}
                        </h3>
                        <p className="text-xs text-[#8b949e] mt-1 max-w-sm mx-auto">
                          {locale === 'zh'
                            ? '请尝试更换搜索条件或清空已选择的媒体库/标签筛选。'
                            : 'Try adjusting your search criteria, clearing tag filters, or selecting all libraries.'}
                        </p>
                      </div>
                      <button
                        onClick={handleResetFilters}
                        className="inline-flex items-center gap-1.5 px-4 py-2 bg-[#161c26] hover:bg-[#232b38] border border-[#232b38] text-xs font-medium rounded-lg text-white transition-colors cursor-pointer"
                      >
                        <RotateCcw className="w-3.5 h-3.5 text-[#00a4dc]" />
                        <span>{locale === 'zh' ? '重置所有筛选' : 'Reset all filters'}</span>
                      </button>
                    </div>
                  ) : (
                    <div className="grid grid-cols-2 sm:grid-cols-3 md:grid-cols-4 lg:grid-cols-5 gap-4">
                      {response.Items.map((person) => (
                        <PersonCard
                          key={person.Id}
                          person={person}
                          onSelect={setSelectedPerson}
                          onToggleFavorite={handleToggleFavorite}
                          locale={locale}
                        />
                      ))}
                    </div>
                  )}
                </>
              )}

              {/* Pagination Controls */}
              {!loading && totalPages > 1 && (
                <div className="mt-8 pt-4 border-t border-[#232b38] flex items-center justify-between flex-wrap gap-4">
                  <div className="text-xs text-[#8b949e]">
                    {locale === 'zh'
                      ? `显示第 ${pageIndex * pageSize + 1} - ${Math.min(
                          (pageIndex + 1) * pageSize,
                          totalRecords
                        )} 条，共 ${totalRecords} 条`
                      : `Showing ${pageIndex * pageSize + 1} - ${Math.min(
                          (pageIndex + 1) * pageSize,
                          totalRecords
                        )} of ${totalRecords}`}
                  </div>

                  <div className="flex items-center gap-2">
                    <button
                      onClick={() => setPageIndex((p) => Math.max(0, p - 1))}
                      disabled={pageIndex === 0}
                      className="flex items-center gap-1 px-3 py-1.5 text-xs font-medium bg-[#161c26] hover:bg-[#1c2432] text-white rounded-lg border border-[#232b38] transition-colors disabled:opacity-30 disabled:cursor-not-allowed cursor-pointer"
                    >
                      <ChevronLeft className="w-4 h-4" />
                      <span>{locale === 'zh' ? '上一页' : 'Previous'}</span>
                    </button>

                    <span className="text-xs font-mono px-3 py-1 bg-[#161c26] border border-[#232b38] rounded-lg text-[#00a4dc]">
                      {pageIndex + 1} / {totalPages}
                    </span>

                    <button
                      onClick={() => setPageIndex((p) => Math.min(totalPages - 1, p + 1))}
                      disabled={pageIndex >= totalPages - 1}
                      className="flex items-center gap-1 px-3 py-1.5 text-xs font-medium bg-[#161c26] hover:bg-[#1c2432] text-white rounded-lg border border-[#232b38] transition-colors disabled:opacity-30 disabled:cursor-not-allowed cursor-pointer"
                    >
                      <span>{locale === 'zh' ? '下一页' : 'Next'}</span>
                      <ChevronRight className="w-4 h-4" />
                    </button>
                  </div>
                </div>
              )}
            </>
          )}
        </main>
      </div>

      {/* Person Detail Modal */}
      {selectedPerson && (
        <PersonDetailModal
          person={selectedPerson}
          onClose={() => setSelectedPerson(null)}
          onToggleFavorite={handleToggleFavorite}
          locale={locale}
        />
      )}

      {/* Settings Modal */}
      {showSettings && (
        <SettingsModal
          config={config}
          libraries={libraries}
          onClose={() => setShowSettings(false)}
          onSave={handleSaveConfig}
          locale={locale}
        />
      )}

      {/* Debug Logs Modal */}
      {showLogs && (
        <DebugLogsModal
          logs={debugLogs}
          onClose={() => setShowLogs(false)}
          onRefresh={fetchDebugLogs}
          locale={locale}
        />
      )}
    </div>
  );
};
export default App;
