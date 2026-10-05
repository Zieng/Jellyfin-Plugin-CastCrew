import React from 'react';
import { X, Heart, MapPin, Calendar, Film, Award, Tag, Camera } from 'lucide-react';
import { PersonItem } from '../types';
import { formatBirthDate, toCountryCode } from '../utils/formatters';

interface PersonDetailModalProps {
  person: PersonItem | null;
  onClose: () => void;
  onToggleFavorite: (id: string, e: React.MouseEvent) => void;
  locale: 'en' | 'zh';
}

export const PersonDetailModal: React.FC<PersonDetailModalProps> = ({
  person,
  onClose,
  onToggleFavorite,
  locale,
}) => {
  if (!person) return null;

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/80 backdrop-blur-sm animate-in fade-in duration-200">
      <div
        className="relative w-full max-w-3xl max-h-[90vh] bg-[#161c26] border border-[#232b38] rounded-2xl shadow-2xl overflow-hidden flex flex-col"
        onClick={(e) => e.stopPropagation()}
      >
        {/* Header bar */}
        <div className="flex items-center justify-between p-4 border-b border-[#232b38] bg-[#0f141c]">
          <div className="flex items-center gap-2">
            <span className="text-xs font-semibold uppercase tracking-wider px-2 py-0.5 rounded bg-[#00a4dc]/20 text-[#00a4dc] border border-[#00a4dc]/30">
              {person.Role}
            </span>
            <span className="text-sm text-[#8b949e]">Jellyfin Native Item Detail</span>
          </div>
          <button
            onClick={onClose}
            className="p-1.5 text-[#8b949e] hover:text-white hover:bg-[#232b38] rounded-lg transition-colors cursor-pointer"
          >
            <X className="w-5 h-5" />
          </button>
        </div>

        {/* Scrollable content */}
        <div className="overflow-y-auto p-6 space-y-6">
          {/* Top bio row */}
          <div className="flex flex-col sm:flex-row gap-6">
            {/* Portrait */}
            <div className="w-full sm:w-48 shrink-0">
              <div className="relative aspect-[3/4] rounded-xl overflow-hidden border border-[#232b38] shadow-lg">
                <img
                  key={`${person.Id}-${person.ImageUrl}`}
                  src={person.ImageUrl}
                  alt={person.Name}
                  referrerPolicy="no-referrer"
                  onError={(e) => {
                    const target = e.currentTarget;
                    if (!target.src.includes('/api/image-proxy') && person.ImageUrl) {
                      target.src = `/api/image-proxy?url=${encodeURIComponent(person.ImageUrl)}`;
                    }
                  }}
                  className="w-full h-full object-cover object-top"
                />
                <button
                  onClick={(e) => onToggleFavorite(person.Id, e)}
                  className={`absolute top-2 right-2 p-2 rounded-full backdrop-blur-md transition-all ${
                    person.IsFavorite ? 'bg-red-500 text-white' : 'bg-black/60 text-[#8b949e] hover:text-white'
                  }`}
                >
                  <Heart className={`w-4 h-4 ${person.IsFavorite ? 'fill-current' : ''}`} />
                </button>
              </div>

              {person.ImageUrl && (
                <div className="mt-2 text-center">
                  <span className="text-[10px] text-emerald-400 inline-flex items-center gap-1 bg-[#0f141c] px-2.5 py-1 rounded-full border border-emerald-500/30 font-medium">
                    <Camera className="w-3 h-3 text-emerald-400" />
                    <span>{locale === 'zh' ? '已同步演员高清封面' : 'Actor Photo Synced'}</span>
                  </span>
                </div>
              )}
            </div>

            {/* Details */}
            <div className="flex-1 space-y-3">
              <h2 className="text-2xl font-bold text-white tracking-tight">{person.Name}</h2>

              {/* Meta pills */}
              <div className="flex flex-wrap items-center gap-2.5 text-xs text-[#8b949e]">
                {person.Age != null && (
                  <div className="flex items-center gap-1.5 bg-[#0f141c] px-2.5 py-1 rounded-md border border-[#232b38]">
                    <span className="text-[#8b949e]">{locale === 'zh' ? '年龄:' : 'Age:'}</span>
                    <strong className="text-white font-mono">{person.Age} {locale === 'zh' ? '岁' : 'yrs'}</strong>
                  </div>
                )}
                {person.HeightCm && (
                  <div className="flex items-center gap-1.5 bg-[#0f141c] px-2.5 py-1 rounded-md border border-[#232b38]">
                    <span className="text-[#8b949e]">{locale === 'zh' ? '身高:' : 'Height:'}</span>
                    <strong className="text-white font-mono">{person.HeightCm} cm</strong>
                  </div>
                )}
                {(person.BirthDate || person.ProductionYear) && (
                  <div className="flex items-center gap-1.5 bg-[#0f141c] px-2.5 py-1 rounded-md border border-[#232b38]">
                    <Calendar className="w-3.5 h-3.5 text-[#00a4dc]" />
                    <span className="text-[#8b949e]">{locale === 'zh' ? '生日:' : 'Birthday:'}</span>
                    <strong className="text-white">
                      {formatBirthDate(person.BirthDate, locale, person.ProductionYear)}
                    </strong>
                  </div>
                )}
                {(person.ProductionLocations?.[0] || person.BirthPlace) && (
                  <div className="flex items-center gap-1.5 bg-[#0f141c] px-2.5 py-1 rounded-md border border-[#232b38]">
                    <MapPin className="w-3.5 h-3.5 text-[#00a4dc]" />
                    <span className="text-[#8b949e]">{locale === 'zh' ? '国家:' : 'Country:'}</span>
                    <span className="font-mono font-bold text-sky-400 bg-sky-950/40 px-1.5 py-0.5 rounded border border-sky-800/40">
                      {toCountryCode(person.ProductionLocations?.[0] || person.BirthPlace)}
                    </span>
                  </div>
                )}
              </div>

              {/* Actor Body Measurements Box (三围) */}
              {(person.BustCm || person.WaistCm || person.HipCm) && (
                <div className="bg-[#0f141c] border border-emerald-500/30 rounded-xl p-3 space-y-1.5">
                  <div className="flex items-center justify-between text-xs font-semibold text-emerald-400">
                    <span>{locale === 'zh' ? '演员身体三围数据' : 'Actor Measurements (BWH)'}</span>
                    <span className="text-[10px] bg-emerald-500/20 px-2 py-0.5 rounded text-emerald-300">
                      {locale === 'zh' ? '已收录' : 'Recorded'}
                    </span>
                  </div>
                  <div className="grid grid-cols-3 gap-2 text-center pt-1 font-mono">
                    <div className="bg-[#161c26] py-1.5 rounded-lg border border-[#232b38]">
                      <span className="text-[11px] text-[#8b949e] block">{locale === 'zh' ? '胸围 (Bust)' : 'Bust'}</span>
                      <strong className="text-white text-sm">{person.BustCm ? `${person.BustCm} cm` : '-'}</strong>
                    </div>
                    <div className="bg-[#161c26] py-1.5 rounded-lg border border-[#232b38]">
                      <span className="text-[11px] text-[#8b949e] block">{locale === 'zh' ? '腰围 (Waist)' : 'Waist'}</span>
                      <strong className="text-white text-sm">{person.WaistCm ? `${person.WaistCm} cm` : '-'}</strong>
                    </div>
                    <div className="bg-[#161c26] py-1.5 rounded-lg border border-[#232b38]">
                      <span className="text-[11px] text-[#8b949e] block">{locale === 'zh' ? '臀围 (Hip)' : 'Hip'}</span>
                      <strong className="text-white text-sm">{person.HipCm ? `${person.HipCm} cm` : '-'}</strong>
                    </div>
                  </div>
                </div>
              )}

              {/* Overview text */}
              <div>
                <h4 className="text-xs uppercase tracking-wider text-[#8b949e] font-semibold mb-1">
                  {locale === 'zh' ? '人物生平' : 'Biography'}
                </h4>
                <p className="text-sm text-[#e6edf3] leading-relaxed">
                  {person.Overview || (locale === 'zh' ? '暂无简介。' : 'No biography available.')}
                </p>
              </div>

              {/* Tags */}
              {person.Tags && person.Tags.length > 0 && (
                <div>
                  <h4 className="text-xs uppercase tracking-wider text-[#8b949e] font-semibold mb-1.5 flex items-center gap-1">
                    <Tag className="w-3 h-3 text-[#00a4dc]" />
                    {locale === 'zh' ? '标签' : 'Tags'}
                  </h4>
                  <div className="flex flex-wrap gap-1.5">
                    {person.Tags.map((tag) => (
                      <span
                        key={tag}
                        className="text-xs bg-[#0f141c] text-[#8b949e] border border-[#232b38] px-2.5 py-0.5 rounded-full"
                      >
                        {tag}
                      </span>
                    ))}
                  </div>
                </div>
              )}
            </div>
          </div>

          {/* Filmography Section */}
          <div className="border-t border-[#232b38] pt-5">
            <div className="flex items-center justify-between mb-4">
              <h3 className="text-base font-semibold text-white flex items-center gap-2">
                <Film className="w-4 h-4 text-[#00a4dc]" />
                {locale === 'zh' ? '参演与创作作品 (影视库)' : 'Known For / Filmography'}
              </h3>
              <span className="text-xs text-[#8b949e]">
                {person.MovieCredits?.length || 0} {locale === 'zh' ? '部作品' : 'credits'}
              </span>
            </div>

            <div className="grid grid-cols-2 sm:grid-cols-4 gap-3">
              {person.MovieCredits?.map((credit) => (
                <div
                  key={credit.id}
                  className="bg-[#0f141c] border border-[#232b38] rounded-xl overflow-hidden p-2 group hover:border-[#00a4dc]/60 transition-colors"
                >
                  <div className="aspect-[2/3] w-full rounded-lg overflow-hidden bg-[#161c26] mb-2">
                    <img
                      src={credit.posterUrl}
                      alt={credit.title}
                      className="w-full h-full object-cover group-hover:scale-105 transition-transform duration-200"
                    />
                  </div>
                  <h5 className="text-xs font-semibold text-white truncate">{credit.title}</h5>
                  <p className="text-[11px] text-[#8b949e] truncate">{credit.role}</p>
                  <p className="text-[10px] text-[#00a4dc] mt-0.5">{credit.year}</p>
                </div>
              ))}
            </div>
          </div>
        </div>

        {/* Footer */}
        <div className="p-4 border-t border-[#232b38] bg-[#0f141c] flex items-center justify-between">
          <span className="text-xs text-[#8b949e]">ID: {person.Id}</span>
          <button
            onClick={onClose}
            className="px-4 py-1.5 text-xs font-medium bg-[#161c26] hover:bg-[#232b38] text-white rounded-lg border border-[#232b38] transition-colors cursor-pointer"
          >
            {locale === 'zh' ? '关闭' : 'Close'}
          </button>
        </div>
      </div>
    </div>
  );
};
