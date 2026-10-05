import React, { useState, useEffect, useCallback } from 'react';
import {
  Search,
  ExternalLink,
  Globe,
  Check,
  DownloadCloud,
  Layers,
  Sparkles,
  Camera,
  Filter,
  ArrowRight,
} from 'lucide-react';
import { MetadataPerson, MetadataSearchResponse, PluginConfiguration } from '../types';
import { toCountryCode } from '../utils/formatters';

interface MetadataSearchTabProps {
  config: PluginConfiguration;
  locale: 'en' | 'zh';
  onActorSyncedToLocal?: (person: any) => void;
  onViewLocalDetail?: (person: any) => void;
}

export const MetadataSearchTab: React.FC<MetadataSearchTabProps> = ({
  config,
  locale,
  onActorSyncedToLocal,
  onViewLocalDetail,
}) => {
  const [name, setName] = useState<string>('');
  const [loading, setLoading] = useState<boolean>(false);
  const [data, setData] = useState<MetadataSearchResponse | null>(null);
  const [error, setError] = useState<string | null>(null);

  // Available provider definitions
  const allProviders = [
    { name: 'TMDB', enabled: config.EnableTmdbProvider, defaultSelect: config.DefaultSelectTmdb },
    { name: 'MASex', enabled: config.EnableMasexProvider, defaultSelect: config.DefaultSelectMasex },
    { name: 'Netflav', enabled: config.EnableNetflavProvider, defaultSelect: config.DefaultSelectNetflav },
  ];

  // Helper to determine initial selected sources based on config
  const computeSelectedSources = useCallback(() => {
    const list: string[] = [];
    if (config.EnableTmdbProvider && config.DefaultSelectTmdb) list.push('TMDB');
    if (config.EnableMasexProvider && config.DefaultSelectMasex) list.push('MASex');
    if (config.EnableNetflavProvider && config.DefaultSelectNetflav) list.push('Netflav');
    // If none are default-selected but some are enabled, fallback to all enabled
    if (list.length === 0) {
      if (config.EnableTmdbProvider) list.push('TMDB');
      if (config.EnableMasexProvider) list.push('MASex');
      if (config.EnableNetflavProvider) list.push('Netflav');
    }
    return list;
  }, [
    config.EnableTmdbProvider,
    config.DefaultSelectTmdb,
    config.EnableMasexProvider,
    config.DefaultSelectMasex,
    config.EnableNetflavProvider,
    config.DefaultSelectNetflav,
  ]);

  // Selected sources state (can be single or multi-select)
  const [selectedSources, setSelectedSources] = useState<string[]>(computeSelectedSources);

  // Sync selected sources when config default selections change
  useEffect(() => {
    setSelectedSources(computeSelectedSources());
  }, [computeSelectedSources]);

  // Syncing to local state
  const [syncingId, setSyncingId] = useState<string | null>(null);
  const [syncedIds, setSyncedIds] = useState<Set<string>>(new Set());
  const [syncedPersonsMap, setSyncedPersonsMap] = useState<Record<string, any>>({});
  const [syncToast, setSyncToast] = useState<string | null>(null);

  const toggleSource = (sourceName: string) => {
    setSelectedSources((prev) =>
      prev.includes(sourceName) ? prev.filter((s) => s !== sourceName) : [...prev, sourceName]
    );
  };

  const handleSelectAllSources = () => {
    const enabled = allProviders.filter((p) => p.enabled).map((p) => p.name);
    setSelectedSources(enabled);
  };

  const doSearch = useCallback(async () => {
    if (selectedSources.length === 0) {
      setData({ Items: [], TotalRecordCount: 0, EnabledSources: [], Errors: [] });
      return;
    }
    setLoading(true);
    setError(null);
    try {
      const q = new URLSearchParams();
      q.set('type', 'Actor');
      if (name) q.set('name', name);
      q.set('sources', selectedSources.join(','));
      q.set('limit', '50');

      const res = await fetch(`/CastCrew/Metadata/Search?${q.toString()}`);
      if (!res.ok) throw new Error(`HTTP ${res.status}`);
      const json: MetadataSearchResponse = await res.json();
      setData(json);
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Search failed');
    } finally {
      setLoading(false);
    }
  }, [name, selectedSources]);

  useEffect(() => {
    doSearch();
  }, [doSearch]);

  const handleApplyToLocal = async (item: MetadataPerson) => {
    setSyncingId(item.Id);
    try {
      const res = await fetch('/CastCrew/Metadata/ApplyToLocal', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          name: item.Name,
          age: item.Age,
          heightCm: item.HeightCm,
          bustCm: item.BustCm,
          waistCm: item.WaistCm,
          hipCm: item.HipCm,
          overview: item.Overview,
          imageUrl: item.ImageUrl,
        }),
      });

      if (!res.ok) throw new Error(`HTTP ${res.status}`);
      const result = await res.json();

      setSyncedIds((prev) => new Set(prev).add(item.Id));
      if (result.person) {
        setSyncedPersonsMap((prev) => ({ ...prev, [item.Id]: result.person }));
      }
      const measurements = [
        item.BustCm ? `胸:${item.BustCm}` : null,
        item.WaistCm ? `腰:${item.WaistCm}` : null,
        item.HipCm ? `臀:${item.HipCm}` : null,
      ]
        .filter(Boolean)
        .join(' / ');

      const toastMsg =
        locale === 'zh'
          ? `已成功将「${item.Name}」的照片封面、年龄（${item.Age || '未知'}）与三围（${measurements || '未知'}）同步写入本地演员表详细页！`
          : `Synced ${item.Name}'s photo, age & measurements into local actor profile!`;
      setSyncToast(toastMsg);
      setTimeout(() => setSyncToast(null), 4000);

      if (onActorSyncedToLocal && result.person) {
        onActorSyncedToLocal(result.person);
      }
    } catch (err) {
      console.error('Failed to sync to local actor:', err);
    } finally {
      setSyncingId(null);
    }
  };

  return (
    <div className="space-y-6">
      {/* Title & info banner */}
      <div className="bg-[#161c26] border border-[#232b38] rounded-2xl p-5 shadow-lg">
        <div className="flex flex-wrap items-center justify-between gap-4">
          <div>
            <div className="flex items-center gap-2">
              <span className="px-2.5 py-0.5 rounded-full text-xs font-semibold bg-emerald-500/20 text-emerald-400 border border-emerald-500/30">
                CastCrew 2.0
              </span>
              <h2 className="text-xl font-bold text-white tracking-tight">
                {locale === 'zh' ? '元数据搜索' : 'Metadata Search'}
              </h2>
            </div>
            <p className="text-xs text-[#8b949e] mt-1 max-w-2xl leading-relaxed">
              {locale === 'zh'
                ? '通过勾选指定外部源（TMDB、MASex、Netflav）搜索演员，多源数据按姓名自动聚合去重，并可一键将照片、年龄与三围指标写入本地演员表。'
                : 'Search actors across selected providers (TMDB, MASex, Netflav), aggregate records, and sync photos, age and measurements into local actor profiles.'}
            </p>
          </div>

          {/* Active provider summary */}
          <div className="flex items-center gap-2 bg-[#0f141c] border border-[#232b38] px-3.5 py-2 rounded-xl text-xs">
            <Globe className="w-4 h-4 text-[#00a4dc]" />
            <span className="text-[#8b949e]">{locale === 'zh' ? '已选搜索源: ' : 'Active Sources: '}</span>
            <div className="flex gap-1.5 font-mono text-white">
              {selectedSources.length > 0 ? (
                selectedSources.map((s) => (
                  <span key={s} className="bg-emerald-500/20 text-emerald-300 px-2 py-0.5 rounded border border-emerald-500/40 text-[11px] font-semibold">
                    {s}
                  </span>
                ))
              ) : (
                <span className="text-amber-400">{locale === 'zh' ? '未选择数据源' : 'None selected'}</span>
              )}
            </div>
          </div>
        </div>
      </div>

      {/* Sync Toast Feedback */}
      {syncToast && (
        <div className="p-3.5 rounded-xl bg-emerald-500/15 border border-emerald-500/40 text-emerald-300 text-xs font-medium flex items-center gap-2.5 animate-in fade-in slide-in-from-top-2 duration-200">
          <Check className="w-4 h-4 text-emerald-400 shrink-0" />
          <span>{syncToast}</span>
        </div>
      )}

      {/* Search Input Box & Source Checkboxes */}
      <div className="bg-[#161c26] border border-[#232b38] rounded-2xl p-4 shadow-md space-y-3.5">
        <div className="flex flex-wrap items-center justify-between gap-3 border-b border-[#232b38] pb-3">
          <label className="block text-xs font-semibold text-white">
            {locale === 'zh' ? '按演员姓名搜索' : 'Search by Actor Name'}
          </label>

          {/* Sources Checkboxes (Multi-select or Single-select) */}
          <div className="flex flex-wrap items-center gap-2 text-xs">
            <span className="text-[#8b949e] flex items-center gap-1 font-medium">
              <Filter className="w-3.5 h-3.5 text-[#00a4dc]" />
              {locale === 'zh' ? '搜索数据源勾选 (可多选/单选):' : 'Data Sources (Multi/Single):'}
            </span>

            <div className="flex items-center gap-2">
              {allProviders.map((provider) => {
                const isEnabledInConfig = provider.enabled;
                const isChecked = selectedSources.includes(provider.name);

                return (
                  <button
                    key={provider.name}
                    type="button"
                    disabled={!isEnabledInConfig}
                    onClick={() => toggleSource(provider.name)}
                    className={`flex items-center gap-1.5 px-3 py-1.5 rounded-xl text-xs font-medium transition-all cursor-pointer border ${
                      !isEnabledInConfig
                        ? 'opacity-35 bg-[#0f141c] text-[#8b949e] border-[#232b38] cursor-not-allowed'
                        : isChecked
                        ? 'bg-emerald-500/20 text-emerald-300 border-emerald-500/50 shadow-sm'
                        : 'bg-[#0f141c] text-[#8b949e] hover:text-white border-[#232b38]'
                    }`}
                    title={
                      !isEnabledInConfig
                        ? (locale === 'zh' ? `${provider.name} 已在插件设置中关闭` : `${provider.name} is disabled in settings`)
                        : (locale === 'zh' ? `点击切换 ${provider.name}` : `Toggle ${provider.name}`)
                    }
                  >
                    <div
                      className={`w-3.5 h-3.5 rounded flex items-center justify-center border text-[9px] ${
                        isChecked && isEnabledInConfig
                          ? 'bg-emerald-500 border-emerald-400 text-black font-bold'
                          : 'border-[#364254] bg-[#161c26]'
                      }`}
                    >
                      {isChecked && isEnabledInConfig && '✓'}
                    </div>
                    <span>{provider.name}</span>
                  </button>
                );
              })}

              <button
                type="button"
                onClick={handleSelectAllSources}
                className="text-[11px] text-[#00a4dc] hover:underline cursor-pointer ml-1"
              >
                {locale === 'zh' ? '全选' : 'Select All'}
              </button>
            </div>
          </div>
        </div>

        {/* Input & Search Button */}
        <div className="flex gap-2">
          <div className="relative flex-1">
            <Search className="w-4 h-4 absolute left-3.5 top-1/2 -translate-y-1/2 text-[#8b949e]" />
            <input
              type="text"
              value={name}
              onChange={(e) => setName(e.target.value)}
              onKeyDown={(e) => e.key === 'Enter' && doSearch()}
              placeholder={
                locale === 'zh'
                  ? '输入演员姓名，如 JULIA, Zendaya, Eimi Fukada, Yua Mikami, Florence Pugh...'
                  : 'Enter actor name, e.g. JULIA, Zendaya, Eimi Fukada...'
              }
              className="w-full bg-[#0f141c] border border-[#232b38] focus:border-[#00a4dc] text-white rounded-xl pl-10 pr-4 py-2.5 text-sm outline-none transition-colors"
            />
          </div>
          <button
            type="button"
            onClick={doSearch}
            disabled={loading || selectedSources.length === 0}
            className="flex items-center gap-1.5 px-6 py-2.5 bg-[#00a4dc] hover:bg-[#1bb2e8] text-white rounded-xl text-xs font-semibold transition-colors shadow-md disabled:opacity-50 cursor-pointer"
          >
            <Search className="w-3.5 h-3.5" />
            <span>
              {loading ? (locale === 'zh' ? '正在查询...' : 'Searching...') : locale === 'zh' ? '搜索演员' : 'Search'}
            </span>
          </button>
        </div>

        {selectedSources.length === 0 && (
          <p className="text-[11px] text-amber-400 bg-amber-500/10 border border-amber-500/30 px-3 py-1.5 rounded-lg">
            {locale === 'zh' ? '⚠️ 当前未勾选任何数据源，请至少勾选一个数据源以进行搜索。' : '⚠️ Please select at least one data source to search.'}
          </p>
        )}
      </div>

      {/* Results Header Status */}
      <div className="flex items-center justify-between text-xs text-[#8b949e] px-1">
        <div>
          {locale === 'zh' ? '检索整合结果: ' : 'Integrated Results: '}
          <strong className="text-white font-mono">{data?.TotalRecordCount || 0}</strong>
          {data?.EnabledSources && data.EnabledSources.length > 0 && (
            <span className="ml-3 text-[#8b949e]">
              {locale === 'zh' ? '生效来源：' : 'Active Sources: '}
              <span className="text-emerald-400 font-medium">{data.EnabledSources.join(', ')}</span>
            </span>
          )}
        </div>
      </div>

      {/* Loading state */}
      {loading && (
        <div className="py-16 text-center space-y-3">
          <div className="w-8 h-8 border-2 border-[#00a4dc]/30 border-t-[#00a4dc] rounded-full animate-spin mx-auto"></div>
          <p className="text-xs text-[#8b949e]">
            {locale === 'zh'
              ? `正在向已勾选的数据源（${selectedSources.join(', ')}）检索演员并进行整合...`
              : 'Fetching and integrating actor records...'}
          </p>
        </div>
      )}

      {/* Results Grid */}
      {!loading && data && (
        <>
          {data.Items.length === 0 ? (
            <div className="py-16 text-center bg-[#161c26]/50 rounded-2xl border border-dashed border-[#232b38] p-8">
              <Layers className="w-10 h-10 text-[#8b949e] mx-auto opacity-50 mb-3" />
              <h3 className="text-sm font-semibold text-white">
                {locale === 'zh' ? '在所选数据源中没有找到匹配的演员信息' : 'No actors found in selected sources'}
              </h3>
              <p className="text-xs text-[#8b949e] mt-1 max-w-sm mx-auto">
                {locale === 'zh'
                  ? '请尝试勾选更多数据源或更换演员名字（如 JULIA、Zendaya、Eimi Fukada）。'
                  : 'Try selecting more sources or entering another actor name.'}
              </p>
            </div>
          ) : (
            <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4 gap-4">
              {data.Items.map((item) => {
                const isSynced = syncedIds.has(item.Id);
                const isSyncing = syncingId === item.Id;

                return (
                  <div
                    key={item.Id}
                    className="bg-[#161c26] border border-[#232b38] hover:border-[#00a4dc] rounded-xl overflow-hidden transition-all duration-200 shadow-md flex flex-col justify-between"
                  >
                    <div>
                      {/* Portrait & Source Badges */}
                      <div className="aspect-[16/10] w-full bg-[#0f141c] overflow-hidden relative">
                        {item.ImageUrl ? (
                          <img
                            key={item.ImageUrl}
                            src={item.ImageUrl}
                            alt={item.Name}
                            loading="lazy"
                            referrerPolicy="no-referrer"
                            onError={(e) => {
                              const target = e.currentTarget;
                              if (!target.src.includes('/api/image-proxy') && item.ImageUrl) {
                                target.src = `/api/image-proxy?url=${encodeURIComponent(item.ImageUrl)}`;
                              }
                            }}
                            className="w-full h-full object-cover object-top"
                          />
                        ) : (
                          <div className="w-full h-full flex items-center justify-center text-xs text-[#8b949e]">
                            {locale === 'zh' ? '暂无海报' : 'No image'}
                          </div>
                        )}
                        <div className="absolute inset-0 bg-gradient-to-t from-[#161c26] via-transparent to-black/40" />

                        <div className="absolute top-2 left-2 flex flex-wrap gap-1">
                          {item.Source.split(',').map((src) => (
                            <span
                              key={src}
                              className="px-2 py-0.5 rounded text-[10px] font-bold uppercase bg-black/80 backdrop-blur text-emerald-400 border border-emerald-500/30 shadow"
                            >
                              {src}
                            </span>
                          ))}
                        </div>

                        {item.ImageUrl && (
                          <div className="absolute bottom-2 right-2 bg-black/75 backdrop-blur text-white text-[10px] px-2 py-0.5 rounded-full flex items-center gap-1 border border-white/20">
                            <Camera className="w-3 h-3 text-[#00a4dc]" />
                            <span>{locale === 'zh' ? '含高清照片' : 'HD Photo'}</span>
                          </div>
                        )}
                      </div>

                      {/* Info & Measurements */}
                      <div className="p-3.5 space-y-3">
                        <div>
                          <h4 className="text-base font-bold text-white truncate" title={item.Name}>
                            {item.Name}
                          </h4>
                          <div className="flex items-center gap-1.5 mt-1">
                            <span
                              className="font-mono uppercase text-[11px] font-bold text-sky-400 bg-sky-950/40 px-1.5 py-0.5 rounded border border-sky-800/40"
                              title={item.Country || 'International'}
                            >
                              {toCountryCode(item.Country || 'WW')}
                            </span>
                          </div>
                        </div>

                        {/* Age & Height */}
                        <div className="flex items-center gap-2 text-xs">
                          <span className="bg-[#0f141c] px-2.5 py-1 rounded-md border border-[#232b38] text-[#8b949e]">
                            {locale === 'zh' ? '年龄: ' : 'Age: '}
                            <strong className="text-white ml-1">
                              {item.Age ? `${item.Age} 岁` : (locale === 'zh' ? '未收录' : 'N/A')}
                            </strong>
                          </span>
                          {item.HeightCm && (
                            <span className="bg-[#0f141c] px-2.5 py-1 rounded-md border border-[#232b38] text-[#8b949e]">
                              {locale === 'zh' ? '身高: ' : 'Height: '}
                              <strong className="text-white ml-1">{item.HeightCm}cm</strong>
                            </span>
                          )}
                        </div>

                        {/* Three Sizes (三围: 胸围 / 腰围 / 臀围) */}
                        <div className="bg-[#0f141c] border border-emerald-500/30 rounded-xl p-2.5 space-y-1">
                          <div className="flex items-center justify-between text-[11px] font-semibold text-emerald-400">
                            <span>{locale === 'zh' ? '三围指标 (B / W / H)' : 'Measurements (B / W / H)'}</span>
                            <Sparkles className="w-3 h-3 text-emerald-400" />
                          </div>
                          <div className="grid grid-cols-3 gap-1 pt-1 text-center font-mono">
                            <div className="bg-[#161c26] py-1 rounded border border-[#232b38]">
                              <span className="text-[10px] text-[#8b949e] block">{locale === 'zh' ? '胸围' : 'Bust'}</span>
                              <strong className="text-white text-xs">{item.BustCm ? `${item.BustCm}cm` : '-'}</strong>
                            </div>
                            <div className="bg-[#161c26] py-1 rounded border border-[#232b38]">
                              <span className="text-[10px] text-[#8b949e] block">{locale === 'zh' ? '腰围' : 'Waist'}</span>
                              <strong className="text-white text-xs">{item.WaistCm ? `${item.WaistCm}cm` : '-'}</strong>
                            </div>
                            <div className="bg-[#161c26] py-1 rounded border border-[#232b38]">
                              <span className="text-[10px] text-[#8b949e] block">{locale === 'zh' ? '臀围' : 'Hip'}</span>
                              <strong className="text-white text-xs">{item.HipCm ? `${item.HipCm}cm` : '-'}</strong>
                            </div>
                          </div>
                        </div>

                        {/* Bio overview snippet */}
                        {item.Overview && (
                          <p className="text-[11px] text-[#8b949e] line-clamp-2 leading-relaxed">
                            {item.Overview}
                          </p>
                        )}
                      </div>
                    </div>

                    {/* Bottom Actions: Write to Local Actor & Open Source */}
                    <div className="p-3.5 pt-0 space-y-2">
                      <button
                        type="button"
                        onClick={() => handleApplyToLocal(item)}
                        disabled={isSyncing}
                        className={`w-full flex items-center justify-center gap-1.5 py-2 px-3 rounded-lg text-xs font-semibold transition-all cursor-pointer shadow-sm ${
                          isSynced
                            ? 'bg-emerald-500/20 text-emerald-300 border border-emerald-500/40'
                            : 'bg-[#00a4dc] hover:bg-[#1bb2e8] text-white'
                        }`}
                      >
                        {isSyncing ? (
                          <span>{locale === 'zh' ? '正在同步照片与三围...' : 'Syncing photo & data...'}</span>
                        ) : isSynced ? (
                          <>
                            <Check className="w-3.5 h-3.5 text-emerald-400" />
                            <span>{locale === 'zh' ? '照片与三围已写入本地' : 'Photo & Data Synced'}</span>
                          </>
                        ) : (
                          <>
                            <DownloadCloud className="w-3.5 h-3.5" />
                            <span>{locale === 'zh' ? '同步照片、年龄与三围至本地' : 'Sync Photo & Measurements'}</span>
                          </>
                        )}
                      </button>

                      {isSynced && syncedPersonsMap[item.Id] && onViewLocalDetail && (
                        <button
                          type="button"
                          onClick={() => onViewLocalDetail(syncedPersonsMap[item.Id])}
                          className="w-full flex items-center justify-center gap-1.5 py-1.5 px-3 rounded-lg text-xs font-semibold bg-[#1c2432] hover:bg-[#232b38] text-white border border-[#232b38] hover:border-[#00a4dc] transition-all cursor-pointer shadow-sm"
                        >
                          <Camera className="w-3.5 h-3.5 text-[#00a4dc]" />
                          <span>{locale === 'zh' ? '立即查看本地演员详细页 (新照片)' : 'View Local Detail (New Photo)'}</span>
                          <ArrowRight className="w-3.5 h-3.5 text-[#8b949e]" />
                        </button>
                      )}

                      {/* Source Direct Links */}
                      <div className="flex flex-wrap items-center justify-between gap-1 pt-1.5 border-t border-[#232b38]/60 text-[11px]">
                        <span className="text-[#8b949e]">{locale === 'zh' ? '源页面直达:' : 'Source links:'}</span>
                        <div className="flex items-center gap-2">
                          {item.Source.includes('Netflav') && (
                            <a
                              href={`https://netflav.com/all?actress=${encodeURIComponent(item.Name)}`}
                              target="_blank"
                              rel="noreferrer"
                              className="text-emerald-400 hover:text-emerald-300 inline-flex items-center gap-0.5 font-medium hover:underline"
                              title={`https://netflav.com/all?actress=${encodeURIComponent(item.Name)}`}
                            >
                              <span>Netflav</span>
                              <ExternalLink className="w-2.5 h-2.5" />
                            </a>
                          )}
                          {item.Source.includes('MASex') && (
                            <a
                              href={`https://masex.tv/zh-CN/actor/${encodeURIComponent(
                                item.Name.toLowerCase().replace(/\s+/g, '-')
                              )}`}
                              target="_blank"
                              rel="noreferrer"
                              className="text-[#00a4dc] hover:text-[#1bb2e8] inline-flex items-center gap-0.5 font-medium hover:underline"
                            >
                              <span>MASex</span>
                              <ExternalLink className="w-2.5 h-2.5" />
                            </a>
                          )}
                          {item.Source.includes('TMDB') && item.ProfileUrl && (
                            <a
                              href={item.ProfileUrl}
                              target="_blank"
                              rel="noreferrer"
                              className="text-amber-400 hover:text-amber-300 inline-flex items-center gap-0.5 font-medium hover:underline"
                            >
                              <span>TMDB</span>
                              <ExternalLink className="w-2.5 h-2.5" />
                            </a>
                          )}
                          {!item.Source.includes('Netflav') &&
                            !item.Source.includes('MASex') &&
                            !item.Source.includes('TMDB') &&
                            item.ProfileUrl && (
                              <a
                                href={item.ProfileUrl}
                                target="_blank"
                                rel="noreferrer"
                                className="inline-flex items-center gap-1 text-[#8b949e] hover:text-[#00a4dc] transition-colors"
                              >
                                <span>{locale === 'zh' ? '查看源档案' : 'View Source'}</span>
                                <ExternalLink className="w-2.5 h-2.5" />
                              </a>
                            )}
                        </div>
                      </div>
                    </div>
                  </div>
                );
              })}
            </div>
          )}
        </>
      )}
    </div>
  );
};
