import React from 'react';
import { Heart, Film, MapPin } from 'lucide-react';
import { PersonItem } from '../types';
import { toCountryCode } from '../utils/formatters';

interface PersonCardProps {
  person: PersonItem;
  onSelect: (person: PersonItem) => void;
  onToggleFavorite: (id: string, e: React.MouseEvent) => void;
  locale: 'en' | 'zh';
}

export const PersonCard: React.FC<PersonCardProps> = ({
  person,
  onSelect,
  onToggleFavorite,
  locale,
}) => {
  return (
    <div
      onClick={() => onSelect(person)}
      className="group relative bg-[#161c26] hover:bg-[#1c2432] border border-[#232b38] hover:border-[#00a4dc] rounded-xl overflow-hidden cursor-pointer transition-all duration-200 hover:-translate-y-1 shadow-md hover:shadow-xl hover:shadow-[#00a4dc]/10 flex flex-col"
    >
      {/* Image container */}
      <div className="relative aspect-[3/4] w-full bg-[#0f141c] overflow-hidden">
        <img
          key={`${person.Id}-${person.ImageUrl}`}
          src={person.ImageUrl}
          alt={person.Name}
          loading="lazy"
          referrerPolicy="no-referrer"
          onError={(e) => {
            const target = e.currentTarget;
            if (!target.src.includes('/api/image-proxy') && person.ImageUrl) {
              target.src = `/api/image-proxy?url=${encodeURIComponent(person.ImageUrl)}`;
            }
          }}
          className="w-full h-full object-cover object-top transition-transform duration-300 group-hover:scale-105"
        />

        {/* Gradient overlay */}
        <div className="absolute inset-0 bg-gradient-to-t from-[#161c26] via-transparent to-black/30" />

        {/* Favorite button */}
        <button
          onClick={(e) => onToggleFavorite(person.Id, e)}
          title={person.IsFavorite ? (locale === 'zh' ? '取消收藏' : 'Remove favorite') : (locale === 'zh' ? '加入收藏' : 'Add favorite')}
          className={`absolute top-2.5 right-2.5 p-2 rounded-full backdrop-blur-md transition-all ${
            person.IsFavorite
              ? 'bg-red-500/80 text-white'
              : 'bg-black/50 text-[#8b949e] hover:text-white hover:bg-black/70'
          }`}
        >
          <Heart className={`w-4 h-4 ${person.IsFavorite ? 'fill-current' : ''}`} />
        </button>

        {/* Role badge */}
        <div className="absolute bottom-2 left-2.5">
          <span className="text-[11px] font-semibold tracking-wide uppercase px-2 py-0.5 rounded bg-[#00a4dc]/90 text-white backdrop-blur shadow-sm">
            {person.Role}
          </span>
        </div>
      </div>

      {/* Info container */}
      <div className="p-3.5 flex-1 flex flex-col justify-between">
        <div>
          <h3 className="font-semibold text-white text-base group-hover:text-[#00a4dc] transition-colors line-clamp-1">
            {person.Name}
          </h3>

          {/* Quick metadata: only show age (no birth year) and 2-letter uppercase country code */}
          <div className="flex items-center gap-2 mt-1 text-xs text-[#8b949e]">
            {person.Age != null && (
              <span className="text-[#e6edf3] font-medium bg-[#0f141c] px-2 py-0.5 rounded border border-[#232b38]">
                {person.Age} {locale === 'zh' ? '岁' : 'yrs'}
              </span>
            )}
            {person.ProductionLocations?.[0] && (
              <span
                className="flex items-center gap-1 font-mono uppercase text-[11px] font-bold text-sky-400 bg-sky-950/40 px-1.5 py-0.5 rounded border border-sky-800/40"
                title={person.ProductionLocations[0]}
              >
                <MapPin className="w-2.5 h-2.5 shrink-0" />
                {toCountryCode(person.ProductionLocations[0])}
              </span>
            )}
          </div>

          {/* Measurements (三围: 胸围 / 腰围 / 臀围) */}
          {(person.BustCm || person.WaistCm || person.HipCm) && (
            <div className="mt-2 flex items-center justify-between text-[11px] bg-[#0f141c] px-2 py-1 rounded-lg border border-emerald-500/30 text-[#8b949e] font-mono">
              <span className="text-emerald-400 font-semibold">{locale === 'zh' ? '三围' : 'BWH'}:</span>
              <span className="text-white font-medium">
                {person.BustCm || '-'}/{person.WaistCm || '-'}/{person.HipCm || '-'}
              </span>
            </div>
          )}

          {/* Bio snippet */}
          <p className="mt-2 text-xs text-[#8b949e] line-clamp-2 leading-relaxed">
            {person.Overview}
          </p>
        </div>

        {/* Known for films count */}
        <div className="mt-3 pt-2.5 border-t border-[#232b38] flex items-center justify-between text-[11px] text-[#8b949e]">
          <span className="flex items-center gap-1 text-[#00a4dc]">
            <Film className="w-3 h-3" />
            {person.MovieCredits?.length || 0} {locale === 'zh' ? '部代表作' : 'titles'}
          </span>
          <span className="text-[#8b949e] group-hover:text-white transition-colors">
            {locale === 'zh' ? '查看详情 →' : 'Details →'}
          </span>
        </div>
      </div>
    </div>
  );
};
