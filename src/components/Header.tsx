import React from 'react';
import { RefreshCw, Settings, Terminal, Globe, Film, CheckCircle2, Download } from 'lucide-react';
import { PluginConfiguration } from '../types';

interface HeaderProps {
  config: PluginConfiguration;
  lastSynced: string | null;
  isRefreshing: boolean;
  onRefreshMapping: () => void;
  onOpenSettings: () => void;
  onOpenLogs: () => void;
  locale: 'en' | 'zh';
  onToggleLocale: () => void;
}

export const Header: React.FC<HeaderProps> = ({
  config,
  lastSynced,
  isRefreshing,
  onRefreshMapping,
  onOpenSettings,
  onOpenLogs,
  locale,
  onToggleLocale,
}) => {
  const formatSyncTime = (iso: string | null) => {
    if (!iso) return locale === 'zh' ? '未同步' : 'Not synced';
    const date = new Date(iso);
    return date.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit', second: '2-digit' });
  };

  return (
    <header className="sticky top-0 z-30 h-16 bg-[#0f141c]/95 backdrop-blur border-b border-[#232b38] px-4 md:px-6 flex items-center justify-between">
      {/* Brand & Left nav info */}
      <div className="flex items-center gap-3">
        <div className="w-9 h-9 rounded-lg bg-gradient-to-br from-[#00a4dc] to-[#aa5cc3] flex items-center justify-center shadow-lg shadow-[#00a4dc]/20">
          <Film className="w-5 h-5 text-white" />
        </div>
        <div>
          <div className="flex items-center gap-2">
            <span className="font-bold text-lg tracking-tight text-white">Cast &amp; Crew</span>
            <span className="text-xs px-2 py-0.5 rounded-full bg-[#00a4dc]/20 text-[#00a4dc] border border-[#00a4dc]/30 font-medium">
              v1.4.0
            </span>
          </div>
          <p className="text-xs text-[#8b949e] hidden sm:block">
            {locale === 'zh' ? 'Jellyfin 演职人员与导演总览' : 'Jellyfin Cast & Crew Explorer'}
          </p>
        </div>
      </div>

      {/* Action buttons & status */}
      <div className="flex items-center gap-2 sm:gap-3">
        {/* Sync status indicator */}
        <div className="hidden md:flex items-center gap-2 text-xs bg-[#161c26] border border-[#232b38] px-3 py-1.5 rounded-lg text-[#8b949e]">
          <CheckCircle2 className="w-3.5 h-3.5 text-emerald-400" />
          <span>{locale === 'zh' ? '已同步: ' : 'Sync: '}</span>
          <span className="text-white font-mono">{formatSyncTime(lastSynced)}</span>
        </div>

        {/* Download .NET 8 Plugin (DLL & PDB) */}
        <a
          href="/download/Jellyfin.Plugin.CastCrew-net8.0.zip"
          download="Jellyfin.Plugin.CastCrew-net8.0.zip"
          title={locale === 'zh' ? '下载 Jellyfin 10.10.x (.NET 8) 插件 (DLL & PDB 打包)' : 'Download Jellyfin 10.10.x (.NET 8) Plugin (DLL & PDB)'}
          className="flex items-center gap-1.5 px-3 py-1.5 text-xs font-semibold bg-[#00a4dc] hover:bg-[#0092c5] text-white rounded-lg shadow-md shadow-[#00a4dc]/20 transition-all cursor-pointer"
        >
          <Download className="w-3.5 h-3.5" />
          <span>{locale === 'zh' ? '下载 net8 插件' : 'Download .NET 8'}</span>
        </a>

        {/* Manual Refresh Button */}
        <button
          onClick={onRefreshMapping}
          disabled={isRefreshing}
          title={locale === 'zh' ? '刷新媒体库人员映射' : 'Rebuild person-to-library mapping'}
          className="flex items-center gap-1.5 px-3 py-1.5 text-xs font-medium bg-[#161c26] hover:bg-[#1c2432] text-[#e6edf3] hover:text-white border border-[#232b38] hover:border-[#00a4dc]/50 rounded-lg transition-colors disabled:opacity-50 cursor-pointer"
        >
          <RefreshCw className={`w-3.5 h-3.5 text-[#00a4dc] ${isRefreshing ? 'animate-spin' : ''}`} />
          <span className="hidden sm:inline">{locale === 'zh' ? '刷新映射' : 'Sync Mapping'}</span>
        </button>

        {/* Debug Logs Viewer Button */}
        {config.EnableDebugLogging && (
          <button
            onClick={onOpenLogs}
            title={locale === 'zh' ? '查看调试日志' : 'View Debug Logs'}
            className="flex items-center gap-1.5 px-3 py-1.5 text-xs font-medium bg-amber-500/10 hover:bg-amber-500/20 text-amber-300 border border-amber-500/30 rounded-lg transition-colors cursor-pointer"
          >
            <Terminal className="w-3.5 h-3.5" />
            <span className="hidden sm:inline">Debug Logs</span>
          </button>
        )}

        {/* Language switch */}
        <button
          onClick={onToggleLocale}
          title={locale === 'zh' ? 'Switch to English' : '切换为中文'}
          className="flex items-center gap-1 px-2.5 py-1.5 text-xs font-medium bg-[#161c26] hover:bg-[#1c2432] text-[#e6edf3] border border-[#232b38] rounded-lg transition-colors cursor-pointer"
        >
          <Globe className="w-3.5 h-3.5 text-[#8b949e]" />
          <span>{locale === 'en' ? '中' : 'EN'}</span>
        </button>

        {/* Settings button */}
        <button
          onClick={onOpenSettings}
          title={locale === 'zh' ? 'CastCrew 配置' : 'CastCrew Settings'}
          className="p-2 text-[#8b949e] hover:text-white bg-[#161c26] hover:bg-[#1c2432] border border-[#232b38] hover:border-[#00a4dc]/50 rounded-lg transition-colors cursor-pointer"
        >
          <Settings className="w-4 h-4" />
        </button>
      </div>
    </header>
  );
};
