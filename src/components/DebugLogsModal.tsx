import React from 'react';
import { X, Terminal, RefreshCw, Trash2 } from 'lucide-react';
import { LogEntry } from '../types';

interface DebugLogsModalProps {
  logs: LogEntry[];
  onClose: () => void;
  onRefresh: () => void;
  locale: 'en' | 'zh';
}

export const DebugLogsModal: React.FC<DebugLogsModalProps> = ({
  logs,
  onClose,
  onRefresh,
  locale,
}) => {
  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/80 backdrop-blur-sm animate-in fade-in duration-200">
      <div
        className="relative w-full max-w-3xl bg-[#0d1117] border border-[#232b38] rounded-2xl shadow-2xl overflow-hidden flex flex-col max-h-[85vh]"
        onClick={(e) => e.stopPropagation()}
      >
        {/* Header */}
        <div className="flex items-center justify-between p-4 border-b border-[#232b38] bg-[#161c26]">
          <div className="flex items-center gap-2">
            <div className="w-7 h-7 rounded-md bg-amber-500/20 text-amber-400 flex items-center justify-center">
              <Terminal className="w-4 h-4" />
            </div>
            <div>
              <h2 className="text-sm font-semibold text-white">
                {locale === 'zh' ? 'CastCrew 插件调试日志' : 'CastCrew Debug Console'}
              </h2>
              <p className="text-[11px] text-[#8b949e]">
                {logs.length} {locale === 'zh' ? '条活动日志记录' : 'events logged'}
              </p>
            </div>
          </div>

          <div className="flex items-center gap-2">
            <button
              onClick={onRefresh}
              title={locale === 'zh' ? '刷新日志' : 'Refresh logs'}
              className="p-1.5 text-[#8b949e] hover:text-white hover:bg-[#232b38] rounded-lg transition-colors cursor-pointer"
            >
              <RefreshCw className="w-4 h-4" />
            </button>
            <button
              onClick={onClose}
              className="p-1.5 text-[#8b949e] hover:text-white hover:bg-[#232b38] rounded-lg transition-colors cursor-pointer"
            >
              <X className="w-5 h-5" />
            </button>
          </div>
        </div>

        {/* Log Viewer */}
        <div className="flex-1 overflow-y-auto p-4 font-mono text-xs space-y-2 bg-[#0a0d12]">
          {logs.length === 0 ? (
            <div className="text-center text-[#8b949e] py-12">
              {locale === 'zh' ? '暂无日志记录。' : 'No logs recorded yet.'}
            </div>
          ) : (
            logs.map((log) => {
              const time = new Date(log.timestamp).toLocaleTimeString();
              const isDebug = log.level === 'Debug';
              const isWarn = log.level === 'Warning';
              const isErr = log.level === 'Error';

              return (
                <div
                  key={log.id}
                  className="flex items-start gap-2.5 p-2 rounded bg-[#0f141c] border border-[#232b38]/60 hover:border-[#232b38] transition-colors"
                >
                  <span className="text-[#8b949e] shrink-0 font-mono text-[11px]">{time}</span>
                  <span
                    className={`px-1.5 py-0.5 rounded text-[10px] font-bold uppercase shrink-0 ${
                      isErr
                        ? 'bg-red-500/20 text-red-400 border border-red-500/30'
                        : isWarn
                        ? 'bg-amber-500/20 text-amber-400 border border-amber-500/30'
                        : isDebug
                        ? 'bg-purple-500/20 text-purple-400 border border-purple-500/30'
                        : 'bg-[#00a4dc]/20 text-[#00a4dc] border border-[#00a4dc]/30'
                    }`}
                  >
                    {log.level}
                  </span>
                  <span className="text-[#e6edf3] break-all leading-relaxed">{log.message}</span>
                </div>
              );
            })
          )}
        </div>

        {/* Footer */}
        <div className="p-3 border-t border-[#232b38] bg-[#161c26] text-xs text-[#8b949e] flex items-center justify-between">
          <span>CastCrewDebugLogging.cs Emulation</span>
          <button
            onClick={onClose}
            className="px-3 py-1 bg-[#0f141c] hover:bg-[#232b38] text-white rounded border border-[#232b38] transition-colors cursor-pointer"
          >
            {locale === 'zh' ? '关闭' : 'Close'}
          </button>
        </div>
      </div>
    </div>
  );
};
