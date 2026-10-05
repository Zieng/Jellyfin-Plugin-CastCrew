import React from 'react';
import { Home, Heart, Clapperboard, Tv, Users, Database, Globe2 } from 'lucide-react';

interface SidebarProps {
  activeSection: string;
  onSelectSection: (section: string) => void;
  showMainMenuEntry: boolean;
  locale: 'en' | 'zh';
}

export const Sidebar: React.FC<SidebarProps> = ({
  activeSection,
  onSelectSection,
  showMainMenuEntry,
  locale,
}) => {
  return (
    <aside className="w-64 bg-[#0f141c] border-r border-[#232b38] flex flex-col shrink-0 min-h-[calc(100vh-4rem)]">
      {/* Jellyfin server header */}
      <div className="p-4 border-b border-[#232b38]/60 flex items-center gap-3">
        <div className="w-8 h-8 rounded-lg bg-gradient-to-tr from-[#00a4dc] to-[#2563eb] flex items-center justify-center font-bold text-white text-sm">
          JF
        </div>
        <div>
          <h2 className="text-sm font-semibold text-white">Jellyfin Media</h2>
          <p className="text-xs text-emerald-400 flex items-center gap-1.5">
            <span className="w-1.5 h-1.5 rounded-full bg-emerald-400"></span>
            <span>CastCrew 2.0</span>
          </p>
        </div>
      </div>

      <div className="p-3 space-y-6 flex-1">
        {/* Navigation Group */}
        <div>
          <p className="text-[11px] font-semibold text-[#8b949e] uppercase tracking-wider px-3 mb-2">
            {locale === 'zh' ? '主导航' : 'Navigation'}
          </p>
          <nav className="space-y-1">
            <button
              onClick={() => onSelectSection('home')}
              className={`w-full flex items-center gap-3 px-3 py-2 rounded-lg text-sm font-medium transition-colors cursor-pointer ${
                activeSection === 'home'
                  ? 'bg-[#161c26] text-[#00a4dc]'
                  : 'text-[#8b949e] hover:bg-[#161c26]/60 hover:text-white'
              }`}
            >
              <Home className="w-4 h-4" />
              <span>{locale === 'zh' ? '主页' : 'Home'}</span>
            </button>

            <button
              onClick={() => onSelectSection('favorites')}
              className={`w-full flex items-center gap-3 px-3 py-2 rounded-lg text-sm font-medium transition-colors cursor-pointer ${
                activeSection === 'favorites'
                  ? 'bg-[#161c26] text-[#00a4dc]'
                  : 'text-[#8b949e] hover:bg-[#161c26]/60 hover:text-white'
              }`}
            >
              <Heart className="w-4 h-4" />
              <span>{locale === 'zh' ? '我的收藏' : 'Favorites'}</span>
            </button>
          </nav>
        </div>

        {/* Plugin Navigation Section */}
        {showMainMenuEntry && (
          <div>
            <div className="flex items-center justify-between px-3 mb-2">
              <p className="text-[11px] font-semibold text-[#00a4dc] uppercase tracking-wider">
                {locale === 'zh' ? 'CastCrew 插件' : 'CastCrew Plugin'}
              </p>
              <span className="text-[10px] bg-emerald-500/20 text-emerald-400 border border-emerald-500/30 px-1.5 py-0.5 rounded font-mono font-bold">
                2.0
              </span>
            </div>
            <div className="space-y-1">
              <button
                onClick={() => onSelectSection('cast_crew')}
                className={`w-full flex items-center justify-between px-3 py-2.5 rounded-lg text-sm font-medium transition-all cursor-pointer ${
                  activeSection === 'cast_crew'
                    ? 'bg-gradient-to-r from-[#00a4dc]/20 to-transparent border-l-4 border-[#00a4dc] text-white font-semibold'
                    : 'text-[#e6edf3] hover:bg-[#161c26] hover:text-white'
                }`}
              >
                <div className="flex items-center gap-3">
                  <Users className={`w-4 h-4 ${activeSection === 'cast_crew' ? 'text-[#00a4dc]' : 'text-[#8b949e]'}`} />
                  <span>{locale === 'zh' ? '演职人员总览' : 'Cast & Crew'}</span>
                </div>
              </button>

              <button
                onClick={() => onSelectSection('metadata_search')}
                className={`w-full flex items-center justify-between px-3 py-2.5 rounded-lg text-sm font-medium transition-all cursor-pointer ${
                  activeSection === 'metadata_search'
                    ? 'bg-gradient-to-r from-emerald-500/20 to-transparent border-l-4 border-emerald-400 text-white font-semibold'
                    : 'text-[#e6edf3] hover:bg-[#161c26] hover:text-white'
                }`}
              >
                <div className="flex items-center gap-3">
                  <Globe2 className={`w-4 h-4 ${activeSection === 'metadata_search' ? 'text-emerald-400' : 'text-[#8b949e]'}`} />
                  <span>{locale === 'zh' ? '元数据搜索' : 'Metadata Search'}</span>
                </div>
                <span className="text-[10px] px-1.5 py-0.2 rounded bg-[#161c26] text-[#8b949e] border border-[#232b38]">
                  NEW
                </span>
              </button>
            </div>
          </div>
        )}

        {/* Media Libraries */}
        <div>
          <p className="text-[11px] font-semibold text-[#8b949e] uppercase tracking-wider px-3 mb-2">
            {locale === 'zh' ? '媒体库' : 'Libraries'}
          </p>
          <div className="space-y-1">
            <button
              onClick={() => onSelectSection('movies')}
              className={`w-full flex items-center gap-3 px-3 py-2 rounded-lg text-sm font-medium transition-colors cursor-pointer ${
                activeSection === 'movies'
                  ? 'bg-[#161c26] text-[#00a4dc]'
                  : 'text-[#8b949e] hover:bg-[#161c26]/60 hover:text-white'
              }`}
            >
              <Clapperboard className="w-4 h-4" />
              <span>{locale === 'zh' ? '电影库' : 'Movies'}</span>
            </button>
            <button
              onClick={() => onSelectSection('tv')}
              className={`w-full flex items-center gap-3 px-3 py-2 rounded-lg text-sm font-medium transition-colors cursor-pointer ${
                activeSection === 'tv'
                  ? 'bg-[#161c26] text-[#00a4dc]'
                  : 'text-[#8b949e] hover:bg-[#161c26]/60 hover:text-white'
              }`}
            >
              <Tv className="w-4 h-4" />
              <span>{locale === 'zh' ? '电视剧' : 'TV Shows'}</span>
            </button>
          </div>
        </div>
      </div>

      {/* Footer system details */}
      <div className="p-3 border-t border-[#232b38] bg-[#0d1117] text-xs text-[#8b949e] flex items-center justify-between">
        <div className="flex items-center gap-2">
          <Database className="w-3.5 h-3.5 text-[#00a4dc]" />
          <span>CastCrew 2.0</span>
        </div>
        <span className="font-mono text-[11px] text-emerald-400">net8.0/9.0</span>
      </div>
    </aside>
  );
};
