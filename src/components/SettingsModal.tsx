import React, { useState } from 'react';
import { X, Plus, Trash2, Check, Settings, Save, AlertCircle, Globe, Shield } from 'lucide-react';
import { PluginConfiguration, CastCrewLibraryOption } from '../types';

interface SettingsModalProps {
  config: PluginConfiguration;
  libraries: CastCrewLibraryOption[];
  onClose: () => void;
  onSave: (newConfig: PluginConfiguration) => Promise<void>;
  locale: 'en' | 'zh';
}

export const SettingsModal: React.FC<SettingsModalProps> = ({
  config,
  libraries,
  onClose,
  onSave,
  locale,
}) => {
  const [formData, setFormData] = useState<PluginConfiguration>({ ...config });
  const [selectedLibToAdd, setSelectedLibToAdd] = useState<string>('');
  const [isSaving, setIsSaving] = useState(false);
  const [saveSuccess, setSaveSuccess] = useState(false);

  const availableLibrariesToAdd = libraries.filter(
    (lib) => !formData.IncludedLibraryIds.includes(lib.Id)
  );

  const handleAddLibrary = () => {
    if (!selectedLibToAdd) return;
    setFormData((prev) => ({
      ...prev,
      IncludedLibraryIds: [...prev.IncludedLibraryIds, selectedLibToAdd],
    }));
    setSelectedLibToAdd('');
  };

  const handleRemoveLibrary = (libId: string) => {
    setFormData((prev) => ({
      ...prev,
      IncludedLibraryIds: prev.IncludedLibraryIds.filter((id) => id !== libId),
    }));
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setIsSaving(true);
    try {
      await onSave(formData);
      setSaveSuccess(true);
      setTimeout(() => {
        setSaveSuccess(false);
        onClose();
      }, 700);
    } finally {
      setIsSaving(false);
    }
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/80 backdrop-blur-sm animate-in fade-in duration-200">
      <div
        className="relative w-full max-w-2xl bg-[#161c26] border border-[#232b38] rounded-2xl shadow-2xl overflow-hidden flex flex-col max-h-[90vh]"
        onClick={(e) => e.stopPropagation()}
      >
        {/* Header */}
        <div className="flex items-center justify-between p-4 border-b border-[#232b38] bg-[#0f141c]">
          <div className="flex items-center gap-2">
            <div className="w-7 h-7 rounded-md bg-[#00a4dc]/20 flex items-center justify-center text-[#00a4dc]">
              <Settings className="w-4 h-4" />
            </div>
            <h2 className="text-base font-semibold text-white">
              {locale === 'zh' ? 'CastCrew 2.0 插件配置' : 'CastCrew 2.0 Configuration'}
            </h2>
            <span className="text-xs px-2 py-0.5 rounded-full bg-emerald-500/20 text-emerald-400 font-mono">
              v2.0.0
            </span>
          </div>
          <button
            onClick={onClose}
            className="p-1.5 text-[#8b949e] hover:text-white hover:bg-[#232b38] rounded-lg transition-colors cursor-pointer"
          >
            <X className="w-5 h-5" />
          </button>
        </div>

        {/* Form Body */}
        <form onSubmit={handleSubmit} className="overflow-y-auto p-6 space-y-6">
          {/* Section: 2.0 External Metadata Providers */}
          <div className="bg-[#0f141c] border border-[#232b38] rounded-xl p-4 space-y-4">
            <div className="flex items-center justify-between">
              <div className="flex items-center gap-2">
                <Globe className="w-4 h-4 text-[#00a4dc]" />
                <h3 className="text-sm font-semibold text-white">
                  {locale === 'zh' ? '外部元数据源 (2.0 新增)' : 'External Metadata Providers (2.0)'}
                </h3>
              </div>
              <span className="text-[11px] text-[#8b949e]">TMDB / MASex / Netflav</span>
            </div>

            {/* TMDB Provider */}
            <div className="space-y-2 pt-2 border-t border-[#232b38]">
              <label className="flex items-center gap-2.5 cursor-pointer">
                <input
                  type="checkbox"
                  checked={formData.EnableTmdbProvider}
                  onChange={(e) =>
                    setFormData((prev) => ({
                      ...prev,
                      EnableTmdbProvider: e.target.checked,
                      DefaultSelectTmdb: e.target.checked ? prev.DefaultSelectTmdb : false,
                    }))
                  }
                  className="rounded border-[#232b38] text-[#00a4dc] bg-[#161c26]"
                />
                <span className="text-xs font-semibold text-white">
                  {locale === 'zh' ? '启用 TMDB 元数据源 (The Movie Database)' : 'Enable TMDB Provider'}
                </span>
              </label>
              {formData.EnableTmdbProvider && (
                <div className="pl-6 space-y-2">
                  <label className="flex items-center gap-2 cursor-pointer text-xs text-[#8b949e] hover:text-white">
                    <input
                      type="checkbox"
                      checked={formData.DefaultSelectTmdb}
                      onChange={(e) =>
                        setFormData((prev) => ({ ...prev, DefaultSelectTmdb: e.target.checked }))
                      }
                      className="rounded border-[#232b38] text-emerald-500 bg-[#161c26]"
                    />
                    <span>{locale === 'zh' ? '在元数据搜索页面默认勾选' : 'Selected by default in search'}</span>
                  </label>
                  <input
                    type="password"
                    placeholder={locale === 'zh' ? 'TMDB API Bearer Token (留空使用演示模式)' : 'TMDB API Bearer Token (or blank for demo)'}
                    value={formData.TmdbApiToken}
                    onChange={(e) =>
                      setFormData((prev) => ({ ...prev, TmdbApiToken: e.target.value }))
                    }
                    className="w-full bg-[#161c26] border border-[#232b38] focus:border-[#00a4dc] text-white rounded-lg px-3 py-1.5 text-xs outline-none"
                  />
                </div>
              )}
            </div>

            {/* MASex Provider */}
            <div className="space-y-2 pt-2 border-t border-[#232b38]">
              <label className="flex items-center gap-2.5 cursor-pointer">
                <input
                  type="checkbox"
                  checked={formData.EnableMasexProvider}
                  onChange={(e) =>
                    setFormData((prev) => ({
                      ...prev,
                      EnableMasexProvider: e.target.checked,
                      DefaultSelectMasex: e.target.checked ? prev.DefaultSelectMasex : false,
                    }))
                  }
                  className="rounded border-[#232b38] text-[#00a4dc] bg-[#161c26]"
                />
                <span className="text-xs font-semibold text-white">
                  {locale === 'zh' ? '启用 MASex 元数据源' : 'Enable MASex Provider'}
                </span>
              </label>
              {formData.EnableMasexProvider && (
                <div className="pl-6 space-y-2">
                  <label className="flex items-center gap-2 cursor-pointer text-xs text-[#8b949e] hover:text-white">
                    <input
                      type="checkbox"
                      checked={formData.DefaultSelectMasex}
                      onChange={(e) =>
                        setFormData((prev) => ({ ...prev, DefaultSelectMasex: e.target.checked }))
                      }
                      className="rounded border-[#232b38] text-emerald-500 bg-[#161c26]"
                    />
                    <span>{locale === 'zh' ? '在元数据搜索页面默认勾选' : 'Selected by default in search'}</span>
                  </label>
                  <input
                    type="text"
                    placeholder="https://masex.tv/zh-CN/actor"
                    value={formData.MasexActorUrl}
                    onChange={(e) =>
                      setFormData((prev) => ({ ...prev, MasexActorUrl: e.target.value }))
                    }
                    className="w-full bg-[#161c26] border border-[#232b38] focus:border-[#00a4dc] text-white rounded-lg px-3 py-1.5 text-xs outline-none font-mono"
                  />
                </div>
              )}
            </div>

            {/* Netflav Provider */}
            <div className="space-y-2 pt-2 border-t border-[#232b38]">
              <label className="flex items-center gap-2.5 cursor-pointer">
                <input
                  type="checkbox"
                  checked={formData.EnableNetflavProvider}
                  onChange={(e) =>
                    setFormData((prev) => ({
                      ...prev,
                      EnableNetflavProvider: e.target.checked,
                      DefaultSelectNetflav: e.target.checked ? prev.DefaultSelectNetflav : false,
                    }))
                  }
                  className="rounded border-[#232b38] text-[#00a4dc] bg-[#161c26]"
                />
                <span className="text-xs font-semibold text-white">
                  {locale === 'zh' ? '启用 Netflav 元数据源' : 'Enable Netflav Provider'}
                </span>
              </label>
              {formData.EnableNetflavProvider && (
                <div className="pl-6 space-y-2">
                  <label className="flex items-center gap-2 cursor-pointer text-xs text-[#8b949e] hover:text-white">
                    <input
                      type="checkbox"
                      checked={formData.DefaultSelectNetflav}
                      onChange={(e) =>
                        setFormData((prev) => ({ ...prev, DefaultSelectNetflav: e.target.checked }))
                      }
                      className="rounded border-[#232b38] text-emerald-500 bg-[#161c26]"
                    />
                    <span>{locale === 'zh' ? '在元数据搜索页面默认勾选' : 'Selected by default in search'}</span>
                  </label>
                  <input
                    type="text"
                    placeholder="https://netflav.com/all?actress="
                    value={formData.NetflavActorUrl}
                    onChange={(e) =>
                      setFormData((prev) => ({ ...prev, NetflavActorUrl: e.target.value }))
                    }
                    className="w-full bg-[#161c26] border border-[#232b38] focus:border-[#00a4dc] text-white rounded-lg px-3 py-1.5 text-xs outline-none font-mono"
                  />
                </div>
              )}
            </div>
          </div>

          {/* Default Page Size */}
          <div>
            <label className="block text-xs font-semibold text-white mb-1.5">
              {locale === 'zh' ? '默认每页数量 (Page Size)' : 'Default Page Size'}
            </label>
            <input
              type="number"
              min={10}
              max={200}
              value={formData.DefaultPageSize}
              onChange={(e) =>
                setFormData((prev) => ({
                  ...prev,
                  DefaultPageSize: Math.max(10, Math.min(200, parseInt(e.target.value, 10) || 50)),
                }))
              }
              className="w-full bg-[#0f141c] border border-[#232b38] focus:border-[#00a4dc] text-white rounded-lg px-3 py-2 text-sm outline-none transition-colors"
            />
            <p className="mt-1 text-xs text-[#8b949e]">
              {locale === 'zh'
                ? '在演员/导演页面未指定分页大小时生效（范围 10-200，默认 50）。'
                : 'Used by the Actors/Directors page if no explicit page size is provided (10–200).'}
            </p>
          </div>

          {/* Default Sort & UI Language */}
          <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
            <div>
              <label className="block text-xs font-semibold text-white mb-1.5">
                {locale === 'zh' ? '默认排序 (Default Sort)' : 'Default Sort'}
              </label>
              <select
                value={formData.DefaultSortBy}
                onChange={(e) => setFormData((prev) => ({ ...prev, DefaultSortBy: e.target.value }))}
                className="w-full bg-[#0f141c] border border-[#232b38] focus:border-[#00a4dc] text-white rounded-lg px-3 py-2 text-sm outline-none transition-colors"
              >
                <option value="Name">{locale === 'zh' ? '按名称 (Name)' : 'Name'}</option>
                <option value="DateCreated">{locale === 'zh' ? '最近添加 (Recently Added)' : 'Recently Added'}</option>
                <option value="Random">{locale === 'zh' ? '随机排序 (Random)' : 'Random'}</option>
              </select>
            </div>

            <div>
              <label className="block text-xs font-semibold text-white mb-1.5">
                {locale === 'zh' ? '界面语言偏好 (UI Language)' : 'UI Language Preference'}
              </label>
              <select
                value={formData.UiLanguage}
                onChange={(e) => setFormData((prev) => ({ ...prev, UiLanguage: e.target.value }))}
                className="w-full bg-[#0f141c] border border-[#232b38] focus:border-[#00a4dc] text-white rounded-lg px-3 py-2 text-sm outline-none transition-colors"
              >
                <option value="Auto">Auto Detect</option>
                <option value="Chinese">中文 (Chinese)</option>
                <option value="English">English</option>
              </select>
            </div>
          </div>

          {/* Checkboxes */}
          <div className="space-y-3 pt-2 border-t border-[#232b38]">
            <label className="flex items-start gap-3 cursor-pointer">
              <input
                type="checkbox"
                checked={formData.EnableCastCrewMainMenuEntry}
                onChange={(e) =>
                  setFormData((prev) => ({ ...prev, EnableCastCrewMainMenuEntry: e.target.checked }))
                }
                className="mt-0.5 rounded border-[#232b38] text-[#00a4dc] focus:ring-0 bg-[#0f141c]"
              />
              <div>
                <span className="text-sm font-medium text-white block">
                  {locale === 'zh' ? '在左侧/顶部显示 Cast & Crew 导航' : 'Show Cast & Crew entry in navigation'}
                </span>
                <span className="text-xs text-[#8b949e]">
                  {locale === 'zh'
                    ? '在 Jellyfin 菜单中注入 Cast & Crew 入口链接。'
                    : 'Adds/removes a Cast & Crew link in the main navigation.'}
                </span>
              </div>
            </label>

            <label className="flex items-start gap-3 cursor-pointer">
              <input
                type="checkbox"
                checked={formData.EnableDebugLogging}
                onChange={(e) =>
                  setFormData((prev) => ({ ...prev, EnableDebugLogging: e.target.checked }))
                }
                className="mt-0.5 rounded border-[#232b38] text-[#00a4dc] focus:ring-0 bg-[#0f141c]"
              />
              <div>
                <span className="text-sm font-medium text-white block">
                  {locale === 'zh' ? '开启详细调试日志 (Debug Logging)' : 'Enable verbose CastCrew debug logging'}
                </span>
                <span className="text-xs text-[#8b949e]">
                  {locale === 'zh'
                    ? '输出详尽的 API 请求参数、外部元数据源聚合与查询过滤日志。'
                    : 'Writes detailed logs for API requests, provider queries, and mapping.'}
                </span>
              </div>
            </label>
          </div>

          {/* Included Libraries */}
          <div className="pt-2 border-t border-[#232b38] space-y-3">
            <div>
              <h3 className="text-sm font-semibold text-white">
                {locale === 'zh' ? '包含的媒体库 (Included Libraries)' : 'Included Libraries'}
              </h3>
              <p className="text-xs text-[#8b949e] mt-0.5">
                {locale === 'zh'
                  ? '选择在本地 Cast & Crew 中展示哪些媒体库中的演职人员。'
                  : 'Select which media libraries to include.'}
              </p>
            </div>

            {/* Picker row */}
            <div className="flex gap-2">
              <select
                value={selectedLibToAdd}
                onChange={(e) => setSelectedLibToAdd(e.target.value)}
                className="flex-1 bg-[#0f141c] border border-[#232b38] focus:border-[#00a4dc] text-white rounded-lg px-3 py-2 text-sm outline-none"
              >
                <option value="">
                  {locale === 'zh' ? '-- 选择要添加的媒体库 --' : '-- Select a library to add --'}
                </option>
                {availableLibrariesToAdd.map((lib) => (
                  <option key={lib.Id} value={lib.Id}>
                    {lib.Name} ({lib.CollectionType})
                  </option>
                ))}
              </select>
              <button
                type="button"
                onClick={handleAddLibrary}
                disabled={!selectedLibToAdd}
                className="flex items-center gap-1 px-4 py-2 bg-[#00a4dc] hover:bg-[#1bb2e8] text-white rounded-lg text-sm font-medium transition-colors disabled:opacity-40 cursor-pointer"
              >
                <Plus className="w-4 h-4" />
                <span>{locale === 'zh' ? '添加' : 'Add'}</span>
              </button>
            </div>

            {/* Included list chips */}
            {formData.IncludedLibraryIds.length > 0 ? (
              <div className="flex flex-wrap gap-2 pt-1">
                {formData.IncludedLibraryIds.map((id) => {
                  const lib = libraries.find((l) => l.Id === id);
                  return (
                    <div
                      key={id}
                      className="flex items-center gap-2 bg-[#0f141c] border border-[#00a4dc]/40 px-3 py-1.5 rounded-lg text-xs text-white"
                    >
                      <span className="font-medium">{lib ? lib.Name : id}</span>
                      <button
                        type="button"
                        onClick={() => handleRemoveLibrary(id)}
                        className="text-[#8b949e] hover:text-red-400 transition-colors"
                      >
                        <Trash2 className="w-3.5 h-3.5" />
                      </button>
                    </div>
                  );
                })}
              </div>
            ) : (
              <div className="bg-[#0f141c] border border-dashed border-[#232b38] rounded-lg p-3 text-xs text-[#8b949e] flex items-center gap-2">
                <AlertCircle className="w-4 h-4 text-[#00a4dc]" />
                <span>
                  {locale === 'zh'
                    ? '当前未选择特定媒体库，已启用全部媒体库检索。'
                    : 'No specific libraries selected. All libraries are included by default.'}
                </span>
              </div>
            )}
          </div>

          {/* Action buttons */}
          <div className="pt-4 border-t border-[#232b38] flex items-center justify-end gap-3">
            <button
              type="button"
              onClick={onClose}
              className="px-4 py-2 text-xs font-medium bg-[#0f141c] hover:bg-[#232b38] text-[#8b949e] hover:text-white rounded-lg border border-[#232b38] transition-colors cursor-pointer"
            >
              {locale === 'zh' ? '取消' : 'Cancel'}
            </button>
            <button
              type="submit"
              disabled={isSaving}
              className="flex items-center gap-1.5 px-5 py-2 text-xs font-medium bg-[#00a4dc] hover:bg-[#1bb2e8] text-white rounded-lg transition-colors shadow-md disabled:opacity-50 cursor-pointer"
            >
              {saveSuccess ? (
                <>
                  <Check className="w-4 h-4 text-emerald-300" />
                  <span>{locale === 'zh' ? '已保存!' : 'Saved!'}</span>
                </>
              ) : (
                <>
                  <Save className="w-4 h-4" />
                  <span>{locale === 'zh' ? '保存设置' : 'Save Changes'}</span>
                </>
              )}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
};
