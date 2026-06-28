(function () {
    'use strict';

    // Guard against double-initialization (issue #8)
    if (window.__castcrewTopBannerInitialized) {
        return;
    }

    window.__castcrewTopBannerInitialized = true;

    var castCrewTabValue = 'cast_crew';
    var castCrewContainerId = 'castcrewHomeContainer';
    var castCrewStylesId = 'castcrew-home-styles';
    var homeSectionsSelector = '.sections.homeSectionsContainer';
    var hiddenNavDataAttr = 'data-castcrew-hidden-nav';
    var hiddenNavVisibilityAttr = 'data-castcrew-hidden-nav-visibility';
    var hiddenNavPointerEventsAttr = 'data-castcrew-hidden-nav-pointer-events';
    var hiddenNavTabindexAttr = 'data-castcrew-hidden-nav-tabindex';
    var castCrewLinkTargetBoundAttr = 'data-castcrew-link-bound';
    var navContainerSelectors = '.mainDrawerPanel a[href], .navMenuOption[href], header a[href], .headerTabs a[href], .sectionTabs a[href]';

    var state = {
        initialized: false,
        activeTab: 'Actors',
        viewMode: 'grid',
        pageSize: 50,
        pageIndex: 0,
        searchTerm: '',
        sortBy: 'Name',
        sortOrder: 'Ascending',
        totalCount: 0,
        items: [],
        descItems: [],
        nameMatchCount: null,
        descMatchCount: null,
        loading: false,
        userId: null,
        authToken: null,
        routePreference: 'Auto',
        selectedLibraryId: '',
        availableLibraries: [],
        libraryMappingLastSyncedUtc: null,
        mappingRefreshInProgress: false
    };

    var refs = null;
    var scheduled = null;
    var lastObserverFireTime = 0;

    function normalizeSortBy(value) {
        if (value === 'DateCreated' || value === 'Random') {
            return value;
        }

        return 'Name';
    }

    function normalizeSortOrder(value) {
        return value === 'Descending' ? 'Descending' : 'Ascending';
    }

    function parseSortSelection(value) {
        var parts = String(value || 'Name,Ascending').split(',');
        return {
            sortBy: normalizeSortBy(parts[0]),
            sortOrder: normalizeSortOrder(parts[1])
        };
    }

    function buildSortSelection(sortBy, sortOrder) {
        var normalizedSortBy = normalizeSortBy(sortBy);
        if (normalizedSortBy === 'Random') {
            return 'Random,Ascending';
        }

        return normalizedSortBy + ',' + normalizeSortOrder(sortOrder);
    }

    function normalizeRoutePreference(value) {
        if (value === 'HashBang' || value === 'Hash') {
            return value;
        }

        return 'Auto';
    }

    function readCredentials() {
        var raw;
        try {
            raw = window.localStorage.getItem('jellyfin_credentials');
        } catch (error) {
            return null;
        }

        if (!raw) {
            return null;
        }

        try {
            var parsed = JSON.parse(raw);
            var servers = Array.isArray(parsed.Servers) ? parsed.Servers.slice() : [];
            servers.sort(function (left, right) {
                var leftLast = left && left.DateLastAccessed ? Number(left.DateLastAccessed) : 0;
                var rightLast = right && right.DateLastAccessed ? Number(right.DateLastAccessed) : 0;
                return rightLast - leftLast;
            });

            var activeServer = null;
            for (var index = 0; index < servers.length; index += 1) {
                if (servers[index] && servers[index].AccessToken) {
                    activeServer = servers[index];
                    break;
                }
            }

            if (!activeServer) {
                return null;
            }

            return {
                accessToken: activeServer.AccessToken || null,
                userId: activeServer.UserId || null
            };
        } catch (error) {
            return null;
        }
    }

    function getCurrentUserId() {
        if (window.ApiClient && typeof window.ApiClient.getCurrentUserId === 'function') {
            return window.ApiClient.getCurrentUserId();
        }

        if (window.ApiClient && window.ApiClient._serverInfo && window.ApiClient._serverInfo.UserId) {
            return window.ApiClient._serverInfo.UserId;
        }

        var credentials = readCredentials();
        return credentials ? credentials.userId : null;
    }

    function resolveAuthToken() {
        if (state.authToken) {
            return state.authToken;
        }

        var credentials = readCredentials();
        state.authToken = credentials ? credentials.accessToken : null;
        return state.authToken;
    }

    function clearCachedAuthToken() {
        state.authToken = null;
        state.initialized = false;
    }

    function normalizeText(value) {
        return String(value || '')
            .replace(/\s+/g, ' ')
            .trim()
            .toLowerCase();
    }

    function elementText(element) {
        return normalizeText(element && element.textContent);
    }

    function readHref(element) {
        if (!element || typeof element.getAttribute !== 'function') {
            return '';
        }

        return normalizeText(element.getAttribute('href'));
    }

    function isActorsNavElement(element) {
        var href = readHref(element);
        var text = elementText(element);
        return href.indexOf('tab=' + castCrewTabValue) !== -1 || text === 'actors';
    }

    function isActorsLinkHrefValue(hrefValue) {
        var href = normalizeText(hrefValue);
        if (!href) {
            return false;
        }

        if (href.indexOf('tab=' + castCrewTabValue) !== -1) {
            return true;
        }

        return href.indexOf('cast_crew.html') !== -1;
    }

    function normalizeActorsLinkTargets() {
        var actorLinks = Array.prototype.slice.call(document.querySelectorAll(navContainerSelectors))
            .filter(function (link) {
                return isActorsLinkHrefValue(link.getAttribute('href'));
            });

        if (!actorLinks.length) {
            actorLinks = Array.prototype.slice.call(document.querySelectorAll('a[href]'))
                .filter(function (link) {
                    return isActorsLinkHrefValue(link.getAttribute('href'));
                });
        }

        actorLinks.forEach(function (link) {
            if (link.getAttribute(castCrewLinkTargetBoundAttr) === 'true') {
                return;
            }

            link.setAttribute(castCrewLinkTargetBoundAttr, 'true');
            link.setAttribute('target', '_self');
            link.removeAttribute('rel');
        });
    }

    function isLikelySidebarNavElement(element) {
        var current = element;
        for (var depth = 0; depth < 10 && current; depth += 1, current = current.parentElement) {
            var className = normalizeText(current.className);
            var id = normalizeText(current.id);

            if (className.indexOf('drawer') !== -1 ||
                className.indexOf('sidebar') !== -1 ||
                className.indexOf('mainmenu') !== -1 ||
                id.indexOf('drawer') !== -1 ||
                id.indexOf('sidebar') !== -1)
            {
                return true;
            }
        }

        return false;
    }

    function shouldHideNativeNavElement(element) {
        if (!element || isActorsNavElement(element)) {
            return false;
        }

        if (isLikelySidebarNavElement(element)) {
            return false;
        }

        var hasNestedNavControls = Array.prototype.slice.call(
            element.querySelectorAll('a[href], button, [role="tab"], [role="link"]'))
            .some(function (child) {
                return child !== element;
            });

        if (hasNestedNavControls) {
            return false;
        }

        var href = readHref(element);

        // Primary detection: href patterns (locale-independent)
        if (href.indexOf('/home') !== -1 && href.indexOf('tab=') === -1) {
            return true;
        }

        if (href.indexOf('tab=1') !== -1) {
            return true;
        }

        if (href === '#' || href === '#/' || href === '#!/') {
            var text = elementText(element);
            return isHomeOrFavoritesText(text);
        }

        // Fallback: tab buttons without href use text/role detection
        var role = element.getAttribute('role');
        if (role === 'tab' || element.classList.contains('emby-tab-button')) {
            var tabText = elementText(element);
            return isHomeOrFavoritesText(tabText);
        }

        return false;
    }

    function isHomeOrFavoritesText(text) {
        if (!text) {
            return false;
        }

        return text === 'home' ||
            text === 'favorite' ||
            text === 'favorites' ||
            text === '首页' ||
            text === '收藏' ||
            text === 'inicio' ||
            text === 'startseite' ||
            text === 'accueil' ||
            text === 'favoris' ||
            text === 'favoriten' ||
            text === 'favoritos' ||
            text === 'ホーム' ||
            text === 'お気に入り' ||
            text === '홈' ||
            text === '즐겨찾기';
    }

    function hideNativeHomeFavoritesNav() {
        var navItems = Array.prototype.slice.call(
            document.querySelectorAll('a[href], button, [role="tab"], [role="link"]'));

        navItems.forEach(function (element) {
            if (!shouldHideNativeNavElement(element)) {
                return;
            }

            if (element.getAttribute(hiddenNavDataAttr) === 'true') {
                return;
            }

            element.setAttribute(hiddenNavDataAttr, 'true');
            element.setAttribute(hiddenNavVisibilityAttr, element.style.visibility || '');
            element.setAttribute(hiddenNavPointerEventsAttr, element.style.pointerEvents || '');

            if (element.hasAttribute('tabindex')) {
                element.setAttribute(hiddenNavTabindexAttr, element.getAttribute('tabindex') || '');
            } else {
                element.setAttribute(hiddenNavTabindexAttr, '__missing__');
            }

            element.style.visibility = 'hidden';
            element.style.pointerEvents = 'none';
            element.setAttribute('aria-hidden', 'true');
            element.setAttribute('tabindex', '-1');
        });
    }

    function restoreNativeHomeFavoritesNav() {
        var hiddenItems = Array.prototype.slice.call(
            document.querySelectorAll('[' + hiddenNavDataAttr + '="true"]'));

        hiddenItems.forEach(function (element) {
            var originalVisibility = element.getAttribute(hiddenNavVisibilityAttr);
            element.style.visibility = originalVisibility || '';

            var originalPointerEvents = element.getAttribute(hiddenNavPointerEventsAttr);
            element.style.pointerEvents = originalPointerEvents || '';

            var originalTabIndex = element.getAttribute(hiddenNavTabindexAttr);
            if (originalTabIndex === '__missing__') {
                element.removeAttribute('tabindex');
            } else if (originalTabIndex !== null) {
                element.setAttribute('tabindex', originalTabIndex);
            }

            element.removeAttribute('aria-hidden');
            element.removeAttribute(hiddenNavDataAttr);
            element.removeAttribute(hiddenNavVisibilityAttr);
            element.removeAttribute(hiddenNavPointerEventsAttr);
            element.removeAttribute(hiddenNavTabindexAttr);
        });
    }

    function parseCurrentRoute() {
        var hash = window.location.hash || '';

        // Support pathname-based routing (Jellyfin experimental app)
        if (!hash && window.location.pathname) {
            var pathname = window.location.pathname.replace(/^\/web\/?/, '/');
            if (pathname.indexOf('/') === 0 && pathname !== '/') {
                var search = window.location.search || '';
                var params = new URLSearchParams(search.replace(/^\?/, ''));
                return {
                    path: pathname,
                    params: params
                };
            }
        }

        var cleaned = hash.replace(/^#!/, '/').replace(/^#/, '');
        if (cleaned.indexOf('/') !== 0) {
            cleaned = '/' + cleaned;
        }

        var routeParts = cleaned.split('?');
        var path = routeParts[0];
        var query = routeParts.length > 1 ? routeParts.slice(1).join('?') : '';
        var params = new URLSearchParams(query);

        return {
            path: path,
            params: params
        };
    }

    function isActorsRoute() {
        var route = parseCurrentRoute();
        return route.path === '/home' && route.params.get('tab') === castCrewTabValue;
    }

    function findHomePage() {
        var pages = Array.prototype.slice.call(document.querySelectorAll('#indexPage.homePage, .homePage'));
        if (!pages.length) {
            return null;
        }

        // Fast path: check cheap signals first to avoid getComputedStyle reflows
        var activePage = pages.find(function (page) {
            if (page.classList.contains('hide') || page.hidden || page.getAttribute('aria-hidden') === 'true') {
                return false;
            }

            // Only call getComputedStyle if cheap checks pass
            if (page.offsetParent === null) {
                return false;
            }

            var style = window.getComputedStyle(page);
            return style.display !== 'none' && style.visibility !== 'hidden';
        });

        if (activePage) {
            return activePage;
        }

        // Fallback: find any non-hidden page without requiring offsetParent
        var visiblePage = pages.find(function (page) {
            return !page.classList.contains('hide') && !page.hidden && page.getAttribute('aria-hidden') !== 'true';
        });

        return visiblePage || pages[pages.length - 1];
    }

    function findDefaultHomeSections(page) {
        if (!page) {
            return [];
        }

        return Array.prototype.slice.call(page.querySelectorAll(homeSectionsSelector))
            .filter(function (section) {
                return section.id !== castCrewContainerId;
            });
    }

    function clampColorChannel(value) {
        return Math.max(0, Math.min(255, Math.round(value)));
    }

    function parseCssColor(value) {
        var match = String(value || '').match(/rgba?\(([^)]+)\)/i);
        if (!match) {
            return null;
        }

        var parts = match[1].match(/[\d.]+/g);
        if (!parts || parts.length < 3) {
            return null;
        }

        var red = Number(parts[0]);
        var green = Number(parts[1]);
        var blue = Number(parts[2]);
        if (isNaN(red) || isNaN(green) || isNaN(blue)) {
            return null;
        }

        var alpha = 1;
        if (parts.length >= 4) {
            alpha = Number(parts[3]);
            if (isNaN(alpha)) {
                alpha = 1;
            }
        }

        return {
            channels: [clampColorChannel(red), clampColorChannel(green), clampColorChannel(blue)],
            alpha: Math.max(0, Math.min(1, alpha))
        };
    }

    function toRgba(channels, alpha) {
        if (!channels || channels.length < 3) {
            return '';
        }

        var normalizedAlpha = Number(alpha);
        if (isNaN(normalizedAlpha)) {
            normalizedAlpha = 1;
        }

        normalizedAlpha = Math.max(0, Math.min(1, normalizedAlpha));
        return 'rgba(' + channels[0] + ',' + channels[1] + ',' + channels[2] + ',' + normalizedAlpha + ')';
    }

    function resolveThemeTextChannels(element) {
        var current = element;
        while (current && current.nodeType === 1) {
            var currentColor = parseCssColor(window.getComputedStyle(current).color);
            if (currentColor) {
                return currentColor.channels;
            }

            current = current.parentElement;
        }

        return [255, 255, 255];
    }

    function resolveThemeBackgroundChannels(element) {
        var current = element;
        while (current && current.nodeType === 1) {
            var backgroundColor = parseCssColor(window.getComputedStyle(current).backgroundColor);
            if (backgroundColor && backgroundColor.alpha > 0.01) {
                return backgroundColor.channels;
            }

            current = current.parentElement;
        }

        var bodyBackground = document.body ? parseCssColor(window.getComputedStyle(document.body).backgroundColor) : null;
        if (bodyBackground && bodyBackground.alpha > 0.01) {
            return bodyBackground.channels;
        }

        var rootBackground = parseCssColor(window.getComputedStyle(document.documentElement).backgroundColor);
        if (rootBackground && rootBackground.alpha > 0.01) {
            return rootBackground.channels;
        }

        return [28, 28, 28];
    }

    function applyThemeAwarePalette(host, page) {
        if (!host || typeof window.getComputedStyle !== 'function') {
            return;
        }

        var reference = page || host;
        var textChannels = resolveThemeTextChannels(reference);
        var backgroundChannels = resolveThemeBackgroundChannels(reference);

        host.style.setProperty('--castcrew-control-bg', toRgba(textChannels, 0.08));
        host.style.setProperty('--castcrew-control-bg-strong', toRgba(textChannels, 0.12));
        host.style.setProperty('--castcrew-control-border', toRgba(textChannels, 0.24));
        host.style.setProperty('--castcrew-menu-bg', toRgba(backgroundChannels, 0.96));
        host.style.setProperty('--castcrew-menu-border', toRgba(textChannels, 0.2));
        host.style.setProperty('--castcrew-menu-text', toRgba(textChannels, 0.96));
    }

    function ensureActorsStyles() {
        if (document.getElementById(castCrewStylesId)) {
            return;
        }

        var style = document.createElement('style');
        style.id = castCrewStylesId;
        style.textContent = [
            '.cast_crew-host { padding: 0 1.25em 1.25em; --castcrew-control-bg: rgba(255,255,255,.07); --castcrew-control-bg-strong: rgba(255,255,255,.12); --castcrew-control-border: rgba(255,255,255,.22); --castcrew-menu-bg: #1c1c1c; --castcrew-menu-border: rgba(255,255,255,.2); --castcrew-menu-text: rgba(255,255,255,.95); }',
            '.castcrew-title-row { display: flex; align-items: center; justify-content: space-between; gap: .6em; }',
            '.castcrew-title-row .sectionTitle { margin: 0; }',
            '.castcrew-title-actions { display: flex; align-items: center; gap: .45em; flex-wrap: wrap; justify-content: flex-end; }',
            '.castcrew-sync-status { color: rgba(255,255,255,.72); font-size: .82em; }',
            '.castcrew-tabs { display: flex; gap: 0; margin-bottom: 1em; border-bottom: 1px solid rgba(255,255,255,.15); }',
            '.castcrew-tab { background: none; border: none; border-bottom: 2px solid transparent; color: rgba(255,255,255,.6); padding: .6em 1.2em; cursor: pointer; font-size: .95em; transition: color .2s, border-color .2s; }',
            '.castcrew-tab:hover { color: rgba(255,255,255,.9); }',
            '.castcrew-tab-active { color: #fff; border-bottom-color: #00a4dc; font-weight: 500; }',
            '.castcrew-toolbar { display: flex; justify-content: space-between; align-items: center; flex-wrap: wrap; gap: .75em; margin-bottom: 1em; }',
            '.castcrew-toolbar-left { display: flex; gap: .5em; align-items: center; }',
            '.castcrew-toolbar-right { display: flex; gap: .5em; align-items: center; position: relative; }',
            '.castcrew-input, .castcrew-select { background: var(--castcrew-control-bg); border: 1px solid var(--castcrew-control-border); color: var(--castcrew-menu-text); border-radius: .4em; padding: .5em .65em; min-height: 2.2em; }',
            '.castcrew-input { min-width: 14em; }',
            '.castcrew-button { border: 1px solid rgba(255,255,255,.2); background: rgba(255,255,255,.08); color: inherit; border-radius: .4em; padding: .5em .85em; cursor: pointer; }',
            '.castcrew-button[disabled] { opacity: .5; cursor: default; }',
            '.castcrew-icon-button { background: none; border: 1px solid rgba(255,255,255,.15); color: rgba(255,255,255,.7); border-radius: .4em; padding: .4em; cursor: pointer; display: flex; align-items: center; justify-content: center; width: 2.2em; height: 2.2em; }',
            '.castcrew-icon-button:hover { color: #fff; border-color: rgba(255,255,255,.4); }',
            '.castcrew-icon-button .material-icons { font-size: 1.2em; }',
            '.castcrew-count { color: rgba(255,255,255,.7); font-size: .9em; white-space: nowrap; }',
            '.castcrew-meta { color: rgba(255,255,255,.75); margin-bottom: .9em; }',
            '.castcrew-state { border: 1px solid rgba(255,255,255,.22); border-radius: .5em; padding: .8em; margin-bottom: .8em; }',
            '.castcrew-state.error { border-color: rgba(255,93,93,.65); color: #ffd6d6; }',
            '.castcrew-grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(180px, 1fr)); gap: .9em; }',
            '.castcrew-grid.castcrew-list-view { grid-template-columns: 1fr; gap: .5em; }',
            '.castcrew-grid.castcrew-list-view .castcrew-card { display: flex; flex-direction: row; }',
            '.castcrew-grid.castcrew-list-view .castcrew-image-wrap { width: 60px; aspect-ratio: 2 / 3; flex-shrink: 0; }',
            '.castcrew-grid.castcrew-list-view .castcrew-info { display: flex; flex-direction: column; justify-content: center; }',
            '.castcrew-card { border: 1px solid rgba(255,255,255,.16); border-radius: .6em; overflow: hidden; background: rgba(255,255,255,.04); cursor: pointer; text-align: left; color: inherit; }',
            '.castcrew-card:hover { border-color: rgba(255,255,255,.4); }',
            '.castcrew-image-wrap { width: 100%; aspect-ratio: 2 / 3; background: rgba(0,0,0,.25); display: flex; align-items: center; justify-content: center; }',
            '.castcrew-image { width: 100%; height: 100%; object-fit: cover; display: block; }',
            '.castcrew-placeholder { color: rgba(255,255,255,.55); font-size: .9em; }',
            '.castcrew-info { padding: .6em .7em .8em; }',
            '.castcrew-name { margin: 0; font-size: 1em; line-height: 1.35; }',
            '.castcrew-overview { margin-top: .45em; font-size: .84em; color: rgba(255,255,255,.7); display: -webkit-box; -webkit-line-clamp: 3; -webkit-box-orient: vertical; overflow: hidden; min-height: 3.2em; }',
            '.castcrew-section-header { font-size: .9em; color: rgba(255,255,255,.6); margin: 1.2em 0 .5em; padding-bottom: .3em; border-bottom: 1px solid rgba(255,255,255,.1); }',
            '.castcrew-match-count { font-weight: 600; color: rgba(255,255,255,.85); }',
            '.castcrew-section-empty { color: rgba(255,255,255,.5); font-size: .9em; padding: .8em 0; margin: 0; }',
            '.castcrew-section-divider { border: none; border-top: 1px solid rgba(255,255,255,.1); margin: 1.5em 0 0.2em; }',
            '.castcrew-filter-menu { position: absolute; right: 0; top: 2.5em; background: var(--castcrew-menu-bg); border: 1px solid var(--castcrew-menu-border); border-radius: .4em; padding: .6em .8em; z-index: 10; min-width: 10em; color: var(--castcrew-menu-text); }',
            '.castcrew-filter-option { display: flex; align-items: center; gap: .5em; color: var(--castcrew-menu-text); font-size: .9em; cursor: pointer; padding: .3em 0; }',
            '.castcrew-filter-label { display: block; color: var(--castcrew-menu-text); opacity: .85; font-size: .78em; margin-top: .55em; margin-bottom: .25em; }',
            '.castcrew-filter-select { width: 100%; background: var(--castcrew-control-bg-strong); border: 1px solid var(--castcrew-control-border); color: var(--castcrew-menu-text); border-radius: .35em; padding: .45em .55em; min-height: 2.1em; }',
            '.castcrew-select option, .castcrew-filter-select option { background: var(--castcrew-menu-bg); color: var(--castcrew-menu-text); }',
            '.castcrew-pagination { margin-top: 1em; display: flex; align-items: center; justify-content: center; gap: .8em; }'
        ].join('');

        document.head.appendChild(style);
    }

    function createActorsContainer(page) {
        var host = document.createElement('div');
        host.id = castCrewContainerId;
        host.className = 'sections homeSectionsContainer cast_crew-host';
        host.style.display = 'none';
        host.innerHTML = '' +
            '<div class=\"sectionTitleContainer castcrew-title-row\">' +
                '<h2 class=\"sectionTitle\">Cast &amp; Crew</h2>' +
                '<div class=\"castcrew-title-actions\">' +
                    '<span id=\"castcrewSyncStatus\" class=\"castcrew-sync-status\">Last synced: pending</span>' +
                    '<button id=\"castcrewRefreshButton\" class=\"castcrew-icon-button\" type=\"button\" title=\"Refresh library mapping\" aria-label=\"Refresh library mapping\">' +
                        '<span class=\"material-icons\">refresh</span>' +
                    '</button>' +
                    '<button id=\"castcrewSettingsButton\" class=\"castcrew-icon-button\" type=\"button\" title=\"CastCrew settings\" aria-label=\"CastCrew settings\">' +
                        '<span class=\"material-icons\">settings</span>' +
                    '</button>' +
                '</div>' +
            '</div>' +
            '<div class=\"castcrew-tabs\" role=\"tablist\">' +
                '<button class=\"castcrew-tab castcrew-tab-active\" type=\"button\" role=\"tab\" data-tab=\"Actors\" aria-selected=\"true\">Actors</button>' +
                '<button class=\"castcrew-tab\" type=\"button\" role=\"tab\" data-tab=\"Directors\" aria-selected=\"false\">Directors</button>' +
                '<button class=\"castcrew-tab\" type=\"button\" role=\"tab\" data-tab=\"Producers\" aria-selected=\"false\">Producers</button>' +
            '</div>' +
            '<div class=\"castcrew-toolbar\">' +
                '<div class=\"castcrew-toolbar-left\">' +
                    '<input id=\"castcrewSearchInput\" class=\"castcrew-input\" type=\"search\" placeholder=\"Search...\" />' +
                    '<button id=\"castcrewSearchButton\" class=\"castcrew-button\" type=\"button\">Search</button>' +
                '</div>' +
                '<div class=\"castcrew-toolbar-right\">' +
                    '<span id=\"castcrewCountIndicator\" class=\"castcrew-count\"></span>' +
                    '<button id=\"castcrewViewToggle\" class=\"castcrew-icon-button\" type=\"button\" title=\"Change view\" aria-label=\"Change view\">' +
                        '<span class=\"material-icons\">view_module</span>' +
                    '</button>' +
                    '<select id=\"castcrewSortSelect\" class=\"castcrew-select\" aria-label=\"Sort\">' +
                        '<option value=\"Name,Ascending\">Name ↑</option>' +
                        '<option value=\"Name,Descending\">Name ↓</option>' +
                        '<option value=\"DateCreated,Descending\">Date Added (Newest)</option>' +
                        '<option value=\"DateCreated,Ascending\">Date Added (Oldest)</option>' +
                        '<option value=\"Random,Ascending\">Random</option>' +
                    '</select>' +
                    '<button id=\"castcrewFilterButton\" class=\"castcrew-icon-button\" type=\"button\" title=\"Filter\" aria-label=\"Filter\">' +
                        '<span class=\"material-icons\">filter_list</span>' +
                    '</button>' +
                    '<div id=\"castcrewFilterMenu\" class=\"castcrew-filter-menu\" hidden>' +
                        '<label class=\"castcrew-filter-label\" for=\"castcrewLibraryFilter\">Library</label>' +
                        '<select id=\"castcrewLibraryFilter\" class=\"castcrew-filter-select\">' +
                            '<option value=\"\">All libraries</option>' +
                        '</select>' +
                        '<label class=\"castcrew-filter-option\"><input type=\"checkbox\" id=\"castcrewFavFilter\" /> Favorites only</label>' +
                        '<label class=\"castcrew-filter-label\" for=\"castcrewTagFilter\">Tags</label>' +
                        '<select id=\"castcrewTagFilter\" class=\"castcrew-filter-select\">' +
                            '<option value=\"\">All tags</option>' +
                        '</select>' +
                        '<label class=\"castcrew-filter-label\" for=\"castcrewCountryFilter\">Country/Region</label>' +
                        '<select id=\"castcrewCountryFilter\" class=\"castcrew-filter-select\">' +
                            '<option value=\"\">All countries/regions</option>' +
                        '</select>' +
                    '</div>' +
                '</div>' +
            '</div>' +
            '<div id=\"castcrewState\" class=\"castcrew-state\" hidden></div>' +
            '<div id=\"castcrewGrid\" class=\"castcrew-grid\" hidden></div>' +
            '<div id=\"castcrewDescGrid\" class=\"castcrew-grid\" hidden></div>' +
            '<div class=\"castcrew-pagination\">' +
                '<button id=\"castcrewPrevButton\" class=\"castcrew-button\" type=\"button\">Previous</button>' +
                '<span id=\"castcrewPageInfo\" class=\"castcrew-meta\"></span>' +
                '<button id=\"castcrewNextButton\" class=\"castcrew-button\" type=\"button\">Next</button>' +
            '</div>';

        page.appendChild(host);
        return host;
    }

    function removeStaleActorsContainers(activePage) {
        var hosts = Array.prototype.slice.call(document.querySelectorAll('#' + castCrewContainerId));
        hosts.forEach(function (host) {
            if (activePage && host.parentElement === activePage) {
                return;
            }

            // Clear refs if they point to elements in the stale container (issue #2)
            if (refs && refs.host === host) {
                refs = null;
            }

            host.remove();
        });
    }

    function ensureActorsContainer(page) {
        removeStaleActorsContainers(page);

        var host = page.querySelector('#' + castCrewContainerId);
        if (!host) {
            host = createActorsContainer(page);
        }

        if (host.dataset.castcrewBound !== 'true') {
            bindActorsEvents(host);
            host.dataset.castcrewBound = 'true';
        }

        applyThemeAwarePalette(host, page);

        return host;
    }

    function bindActorsEvents(host) {
        refs = {
            host: host,
            searchInput: host.querySelector('#castcrewSearchInput'),
            sortSelect: host.querySelector('#castcrewSortSelect'),
            searchButton: host.querySelector('#castcrewSearchButton'),
            syncStatus: host.querySelector('#castcrewSyncStatus'),
            refreshButton: host.querySelector('#castcrewRefreshButton'),
            settingsButton: host.querySelector('#castcrewSettingsButton'),
            countIndicator: host.querySelector('#castcrewCountIndicator'),
            viewToggle: host.querySelector('#castcrewViewToggle'),
            filterButton: host.querySelector('#castcrewFilterButton'),
            filterMenu: host.querySelector('#castcrewFilterMenu'),
            libraryFilter: host.querySelector('#castcrewLibraryFilter'),
            favFilter: host.querySelector('#castcrewFavFilter'),
            tagFilter: host.querySelector('#castcrewTagFilter'),
            countryFilter: host.querySelector('#castcrewCountryFilter'),
            state: host.querySelector('#castcrewState'),
            grid: host.querySelector('#castcrewGrid'),
            descGrid: host.querySelector('#castcrewDescGrid'),
            prevButton: host.querySelector('#castcrewPrevButton'),
            nextButton: host.querySelector('#castcrewNextButton'),
            pageInfo: host.querySelector('#castcrewPageInfo')
        };

        // Tab click handling
        var tabs = Array.prototype.slice.call(host.querySelectorAll('.castcrew-tab'));
        tabs.forEach(function (tab) {
            tab.addEventListener('click', function () {
                var tabName = tab.getAttribute('data-tab');
                if (tabName === state.activeTab) {
                    return;
                }

                state.activeTab = tabName;
                state.pageIndex = 0;
                state.searchTerm = '';
                refs.searchInput.value = '';

                tabs.forEach(function (t) {
                    t.classList.remove('castcrew-tab-active');
                    t.setAttribute('aria-selected', 'false');
                });
                tab.classList.add('castcrew-tab-active');
                tab.setAttribute('aria-selected', 'true');

                fetchActors();
            });
        });

        refs.searchButton.addEventListener('click', runSearch);
        refs.refreshButton.addEventListener('click', refreshLibraryMapping);
        refs.settingsButton.addEventListener('click', openCastCrewSettings);
        refs.searchInput.addEventListener('keydown', function (event) {
            if (event.key === 'Enter') {
                runSearch();
            }
        });
        refs.sortSelect.addEventListener('change', function () {
            state.pageIndex = 0;
            fetchActors();
        });

        // View toggle (grid ↔ list)
        refs.viewToggle.addEventListener('click', function () {
            state.viewMode = state.viewMode === 'list' ? 'grid' : 'list';
            var icon = refs.viewToggle.querySelector('.material-icons');
            if (state.viewMode === 'list') {
                icon.textContent = 'view_list';
                refs.grid.classList.add('castcrew-list-view');
                if (refs.descGrid) refs.descGrid.classList.add('castcrew-list-view');
            } else {
                icon.textContent = 'view_module';
                refs.grid.classList.remove('castcrew-list-view');
                if (refs.descGrid) refs.descGrid.classList.remove('castcrew-list-view');
            }
        });

        // Filter toggle
        refs.filterButton.addEventListener('click', function () {
            refs.filterMenu.hidden = !refs.filterMenu.hidden;
        });
        refs.libraryFilter.addEventListener('change', function () {
            state.selectedLibraryId = refs.libraryFilter.value || '';
            state.pageIndex = 0;
            fetchActors();
        });
        refs.favFilter.addEventListener('change', function () {
            state.pageIndex = 0;
            fetchActors();
        });
        refs.tagFilter.addEventListener('change', function () {
            state.pageIndex = 0;
            fetchActors();
        });
        refs.countryFilter.addEventListener('change', function () {
            state.pageIndex = 0;
            fetchActors();
        });

        refs.prevButton.addEventListener('click', function () {
            if (state.pageIndex <= 0) {
                return;
            }

            state.pageIndex -= 1;
            fetchActors();
        });

        refs.nextButton.addEventListener('click', function () {
            var totalPages = Math.max(1, Math.ceil(state.totalCount / state.pageSize));
            if ((state.pageIndex + 1) >= totalPages) {
                return;
            }

            state.pageIndex += 1;
            fetchActors();
        });

        renderSyncStatus();
        updateRefreshButtonState();
    }

    function showState(message, isError) {
        if (!refs) {
            return;
        }

        refs.state.hidden = false;
        refs.state.className = isError ? 'castcrew-state error' : 'castcrew-state';
        refs.state.textContent = message;
        refs.grid.hidden = true;
    }

    function clearState() {
        if (!refs) {
            return;
        }

        refs.state.hidden = true;
        refs.state.className = 'castcrew-state';
        refs.state.textContent = '';
    }

    function replaceFilterSelectOptions(selectEl, values, defaultLabel) {
        if (!selectEl) {
            return;
        }

        var previousValue = selectEl.value || '';
        var nextValues = Array.isArray(values) ? values : [];

        selectEl.innerHTML = '';

        var defaultOption = document.createElement('option');
        defaultOption.value = '';
        defaultOption.textContent = defaultLabel;
        selectEl.appendChild(defaultOption);

        nextValues.forEach(function (value) {
            if (!value) {
                return;
            }

            var option = document.createElement('option');
            option.value = value;
            option.textContent = value;
            selectEl.appendChild(option);
        });

        selectEl.value = previousValue;
        if (selectEl.value !== previousValue) {
            selectEl.value = '';
        }
    }

    function formatSyncTimestamp(timestampUtc) {
        if (!timestampUtc) {
            return '';
        }

        var parsed = new Date(timestampUtc);
        if (Number.isNaN(parsed.getTime())) {
            return '';
        }

        return parsed.toLocaleString();
    }

    function renderSyncStatus() {
        if (!refs || !refs.syncStatus) {
            return;
        }

        var formatted = formatSyncTimestamp(state.libraryMappingLastSyncedUtc);
        refs.syncStatus.textContent = formatted
            ? 'Last synced at ' + formatted
            : 'Last synced: pending';
    }

    function updateRefreshButtonState() {
        if (!refs || !refs.refreshButton) {
            return;
        }

        refs.refreshButton.disabled = state.loading || state.mappingRefreshInProgress;
    }

    function replaceLibraryFilterOptions(selectEl, libraries, defaultLabel) {
        if (!selectEl) {
            return;
        }

        var previousValue = selectEl.value || state.selectedLibraryId || '';
        var nextLibraries = Array.isArray(libraries) ? libraries : [];

        selectEl.innerHTML = '';

        var defaultOption = document.createElement('option');
        defaultOption.value = '';
        defaultOption.textContent = defaultLabel;
        selectEl.appendChild(defaultOption);

        nextLibraries.forEach(function (library) {
            if (!library || !library.Id) {
                return;
            }

            var option = document.createElement('option');
            option.value = String(library.Id);
            option.textContent = library.Name ? String(library.Name) : String(library.Id);
            selectEl.appendChild(option);
        });

        selectEl.value = previousValue;
        if (selectEl.value !== previousValue) {
            selectEl.value = '';
        }

        state.selectedLibraryId = selectEl.value || '';
    }

    function updateFilterOptions(payload) {
        if (!refs) {
            return;
        }

        var availableLibraries = payload && Array.isArray(payload.AvailableLibraries)
            ? payload.AvailableLibraries
            : [];
        var availableTags = payload && Array.isArray(payload.AvailableTags) ? payload.AvailableTags : [];
        var availableLocations = payload && Array.isArray(payload.AvailableProductionLocations)
            ? payload.AvailableProductionLocations
            : [];

        state.availableLibraries = availableLibraries;
        replaceLibraryFilterOptions(refs.libraryFilter, availableLibraries, 'All libraries');
        replaceFilterSelectOptions(refs.tagFilter, availableTags, 'All tags');
        replaceFilterSelectOptions(refs.countryFilter, availableLocations, 'All countries/regions');
    }

    function renderMeta() {
        if (!refs) {
            return;
        }

        if (state.loading) {
            refs.countIndicator.textContent = 'Loading...';
        } else if (state.totalCount === 0) {
            refs.countIndicator.textContent = '0 items';
        } else {
            var startNum = state.pageIndex * state.pageSize + 1;
            var endNum = Math.min(startNum + state.items.length - 1, state.totalCount);
            refs.countIndicator.textContent = startNum + '-' + endNum + ' of ' + state.totalCount;
        }
    }

    function renderPagination() {
        if (!refs) {
            return;
        }

        var totalPages = Math.max(1, Math.ceil(state.totalCount / state.pageSize));
        refs.pageInfo.textContent = 'Page ' + (state.pageIndex + 1) + ' / ' + totalPages;
        refs.prevButton.disabled = state.loading || state.pageIndex <= 0;
        refs.nextButton.disabled = state.loading || (state.pageIndex + 1) >= totalPages;
    }

    function escapeHtml(value) {
        return String(value || '')
            .replace(/&/g, '&amp;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;')
            .replace(/'/g, '&#39;');
    }

    function buildActorsUrl() {
        var sortValue = refs && refs.sortSelect
            ? refs.sortSelect.value
            : buildSortSelection(state.sortBy, state.sortOrder);
        var selection = parseSortSelection(sortValue);
        state.sortBy = selection.sortBy;
        state.sortOrder = selection.sortOrder;

        var params = new URLSearchParams();
        params.set('startIndex', String(state.pageIndex * state.pageSize));
        params.set('limit', String(state.pageSize));
        params.set('sortBy', state.sortBy);
        params.set('sortOrder', state.sortOrder);
        if (state.userId) {
            params.set('userId', state.userId);
        }
        if (state.searchTerm) {
            params.set('searchTerm', state.searchTerm);
        }
        if (refs && refs.favFilter && refs.favFilter.checked) {
            params.set('isFavorite', 'true');
        }
        if (refs && refs.tagFilter && refs.tagFilter.value) {
            params.set('tag', refs.tagFilter.value);
        }
        if (refs && refs.countryFilter && refs.countryFilter.value) {
            params.set('productionLocation', refs.countryFilter.value);
        }
        if (refs && refs.libraryFilter && refs.libraryFilter.value) {
            params.set('libraryIds', refs.libraryFilter.value);
        }

        var endpoint = '/CastCrew/' + state.activeTab;
        return endpoint + '?' + params.toString();
    }

    function buildRequestHeaders() {
        if (window.ApiClient && typeof window.ApiClient.getRequestHeaders === 'function') {
            return window.ApiClient.getRequestHeaders();
        }

        var headers = { Accept: 'application/json' };
        var token = resolveAuthToken();
        if (token) {
            headers['X-Emby-Token'] = token;
        }

        return headers;
    }

    function postRequest(url) {
        if (window.ApiClient && typeof window.ApiClient.ajax === 'function') {
            return Promise.resolve(window.ApiClient.ajax({
                type: 'POST',
                url: url
            }));
        }

        return fetch(url, {
            method: 'POST',
            headers: buildRequestHeaders()
        }).then(function (response) {
            if (!response.ok) {
                throw new Error('Request failed with status ' + response.status);
            }

            return response;
        });
    }

    function getPrimaryImageUrl(person) {
        if (!person || !person.Id) {
            return '';
        }

        var imageTag = person.PrimaryImageTag;
        if (!imageTag && person.ImageTags && person.ImageTags.Primary) {
            imageTag = person.ImageTags.Primary;
        }

        if (!imageTag) {
            return '';
        }

        if (window.ApiClient && typeof window.ApiClient.getUrl === 'function') {
            return window.ApiClient.getUrl('Items/' + person.Id + '/Images/Primary', {
                maxHeight: 480,
                quality: 90,
                tag: imageTag
            });
        }

        var params = new URLSearchParams();
        params.set('maxHeight', '480');
        params.set('quality', '90');
        params.set('tag', imageTag);

        var token = resolveAuthToken();
        if (token) {
            params.set('api_key', token);
        }

        return '/Items/' + encodeURIComponent(person.Id) + '/Images/Primary?' + params.toString();
    }

    function getRoutePrefixes() {
        var hash = window.location && typeof window.location.hash === 'string'
            ? window.location.hash
            : '';

        if (state.routePreference === 'Hash') {
            return ['#/', '#!/'];
        }

        if (state.routePreference === 'HashBang') {
            return ['#!/', '#/'];
        }

        if (hash.indexOf('#/') === 0) {
            return ['#/', '#!/'];
        }

        return ['#!/', '#/'];
    }

    function getDetailPathOrder() {
        var hash = window.location && typeof window.location.hash === 'string'
            ? window.location.hash.toLowerCase()
            : '';

        if (hash.indexOf('/person') !== -1) {
            return ['person', 'details', 'item'];
        }

        if (hash.indexOf('/item') !== -1) {
            return ['item', 'details', 'person'];
        }

        return ['details', 'item', 'person'];
    }

    function buildPersonRoutes(personId) {
        var encodedPersonId = encodeURIComponent(personId);
        var prefixes = getRoutePrefixes();
        var detailPaths = getDetailPathOrder().map(function (path) {
            return path + '?id=' + encodedPersonId;
        });
        var routes = [];

        prefixes.forEach(function (prefix) {
            detailPaths.forEach(function (path) {
                routes.push(prefix + path);
            });
        });

        return routes;
    }

    function openPersonDetail(person) {
        if (!person || !person.Id) {
            return;
        }

        var routes = buildPersonRoutes(person.Id);
        var preferredRoute = routes[0];

        if (window.Dashboard && typeof window.Dashboard.navigate === 'function') {
            window.Dashboard.navigate(preferredRoute);
            return;
        }

        if (window.location && typeof window.location.hash === 'string') {
            window.location.hash = preferredRoute;
            return;
        }

        window.location.href = '/web/index.html' + preferredRoute;
    }

    function buildSettingsRoute() {
        var hash = window.location && typeof window.location.hash === 'string'
            ? window.location.hash
            : '';
        var prefix = hash.indexOf('#/') === 0 ? '#/' : '#!/';
        return prefix + 'configurationpage?name=castcrew-config';
    }

    function openCastCrewSettings() {
        var route = buildSettingsRoute();

        if (window.Dashboard && typeof window.Dashboard.navigate === 'function') {
            window.Dashboard.navigate(route);
            return;
        }

        if (window.location && typeof window.location.hash === 'string') {
            window.location.hash = route;
            return;
        }

        window.location.href = '/web/index.html' + route;
    }

    function refreshLibraryMapping() {
        if (!refs || state.mappingRefreshInProgress) {
            return;
        }

        state.mappingRefreshInProgress = true;
        updateRefreshButtonState();

        var endpoint = '/CastCrew/Libraries/RefreshMapping?reason=home-refresh-button';
        postRequest(endpoint)
            .then(function () {
                return fetchActors();
            })
            .catch(function (error) {
                var errorMessage = error && error.message ? error.message : 'Unknown error';
                if (refs && refs.syncStatus) {
                    refs.syncStatus.textContent = 'Last sync refresh failed (' + errorMessage + ')';
                }
            })
            .finally(function () {
                state.mappingRefreshInProgress = false;
                updateRefreshButtonState();
            });
    }

    function renderCardHtml(person) {
        var imageUrl = getPrimaryImageUrl(person);
        var name = person.Name || '';
        var overview = person.Overview
            ? escapeHtml(person.Overview)
            : 'No biography available.';
        var imageHtml = imageUrl
            ? '<img class=\"castcrew-image\" src=\"' + escapeHtml(imageUrl) + '\" alt=\"' + escapeHtml(name) + '\">'
            : '<span class=\"castcrew-placeholder\">No image</span>';

        return '' +
            '<button class=\"castcrew-card\" type=\"button\" data-person-id=\"' + escapeHtml(person.Id) + '\">' +
                '<div class=\"castcrew-image-wrap\">' + imageHtml + '</div>' +
                '<div class=\"castcrew-info\">' +
                    '<h3 class=\"castcrew-name\">' + escapeHtml(name) + '</h3>' +
                    '<div class=\"castcrew-overview\">' + overview + '</div>' +
                '</div>' +
            '</button>';
    }

    function bindCardClicks(container, itemsArray) {
        Array.prototype.slice.call(container.querySelectorAll('.castcrew-card')).forEach(function (button) {
            button.addEventListener('click', function () {
                var personId = button.getAttribute('data-person-id');
                var person = itemsArray.find(function (item) {
                    return item.Id === personId;
                });
                openPersonDetail(person);
            });
        });
    }

    function renderGrid() {
        if (!refs) {
            return;
        }

        var hasDescResults = state.descItems && state.descItems.length > 0;
        var isSearchMode = !!state.searchTerm;

        // Non-search mode: flat list (existing behavior)
        if (!isSearchMode) {
            if (!state.items.length) {
                showState('No results found.', false);
                refs.descGrid.hidden = true;
                return;
            }

            clearState();
            refs.grid.hidden = false;
            refs.grid.innerHTML = state.items.map(renderCardHtml).join('');
            bindCardClicks(refs.grid, state.items);
            refs.descGrid.hidden = true;

            if (state.viewMode === 'list') {
                refs.grid.classList.add('castcrew-list-view');
            }
            return;
        }

        // Search mode: always show both sections with counts
        clearState();

        var nameCount = typeof state.nameMatchCount === 'number'
            ? state.nameMatchCount
            : state.items.length;
        var descCount = typeof state.descMatchCount === 'number'
            ? state.descMatchCount
            : (state.descItems ? state.descItems.length : 0);

        // Name matches section
        refs.grid.insertAdjacentHTML('beforebegin',
            '<div class="castcrew-section-header" id="castcrewNameHeader">Name matches: <span class="castcrew-match-count">' + nameCount + ' Found</span></div>');

        if (nameCount > 0) {
            refs.grid.hidden = false;
            refs.grid.innerHTML = state.items.map(renderCardHtml).join('');
            bindCardClicks(refs.grid, state.items);
        } else {
            refs.grid.hidden = false;
            refs.grid.innerHTML = '<p class="castcrew-section-empty">No name matches found.</p>';
        }

        // Section divider
        refs.descGrid.insertAdjacentHTML('beforebegin',
            '<hr class="castcrew-section-divider" />' +
            '<div class="castcrew-section-header" id="castcrewDescHeader">Description matches: <span class="castcrew-match-count">' + descCount + ' Found</span></div>');

        // Description matches section
        if (descCount > 0) {
            refs.descGrid.hidden = false;
            refs.descGrid.innerHTML = state.descItems.map(renderCardHtml).join('');
            bindCardClicks(refs.descGrid, state.descItems);
        } else {
            refs.descGrid.hidden = false;
            refs.descGrid.innerHTML = '<p class="castcrew-section-empty">No description matches found.</p>';
        }

        // Apply view mode
        if (state.viewMode === 'list') {
            refs.grid.classList.add('castcrew-list-view');
            refs.descGrid.classList.add('castcrew-list-view');
        }
    }

    function fetchActors(retryOnAuth) {
        if (!refs) {
            return Promise.resolve();
        }

        // Clear existing display immediately (Bug #1 fix)
        refs.grid.hidden = true;
        refs.grid.innerHTML = '';
        refs.descGrid.hidden = true;
        refs.descGrid.innerHTML = '';
        clearState();

        // Remove stale section headers and dividers
        var oldHeaders = refs.host.querySelectorAll('.castcrew-section-header, .castcrew-section-divider');
        Array.prototype.slice.call(oldHeaders).forEach(function (h) { h.remove(); });

        state.loading = true;
        state.descItems = [];
        state.nameMatchCount = null;
        state.descMatchCount = null;
        renderMeta();
        renderPagination();
        updateRefreshButtonState();

        var url = buildActorsUrl();
        var requestPromise;
        if (window.ApiClient && typeof window.ApiClient.getJSON === 'function') {
            requestPromise = window.ApiClient.getJSON(url);
        } else {
            requestPromise = fetch(url, {
                method: 'GET',
                headers: buildRequestHeaders()
            }).then(function (response) {
                if (response.status === 401 && retryOnAuth !== false) {
                    clearCachedAuthToken();
                    initializeActorsState();
                    return fetch(url, {
                        method: 'GET',
                        headers: buildRequestHeaders()
                    }).then(function (retryResponse) {
                        if (!retryResponse.ok) {
                            throw new Error('Request failed with status ' + retryResponse.status);
                        }

                        return retryResponse.json();
                    });
                }

                if (response.status === 404) {
                    throw new Error('CastCrew plugin endpoint not available. Ensure the plugin is installed and enabled on the server.');
                }

                if (!response.ok) {
                    throw new Error('Request failed with status ' + response.status);
                }

                return response.json();
            });
        }

        return requestPromise
            .then(function (payload) {
                state.items = payload && payload.Items ? payload.Items : [];
                state.totalCount = payload && typeof payload.TotalRecordCount === 'number'
                    ? payload.TotalRecordCount
                    : 0;

                if (payload && typeof payload.PageSize === 'number' && payload.PageSize > 0) {
                    state.pageSize = payload.PageSize;
                }

                if (payload && typeof payload.StartIndex === 'number' && state.pageSize > 0) {
                    state.pageIndex = Math.floor(payload.StartIndex / state.pageSize);
                }

                state.libraryMappingLastSyncedUtc = payload && payload.LibraryMappingLastSyncedUtc
                    ? payload.LibraryMappingLastSyncedUtc
                    : null;
                renderSyncStatus();
                updateFilterOptions(payload);

                state.sortBy = normalizeSortBy(payload && payload.SortBy ? payload.SortBy : state.sortBy);
                state.sortOrder = normalizeSortOrder(payload && payload.SortOrder ? payload.SortOrder : state.sortOrder);
                state.routePreference = normalizeRoutePreference(
                    payload && payload.DetailRoutePreference ? payload.DetailRoutePreference : state.routePreference);

                if (refs) {
                    refs.sortSelect.value = buildSortSelection(state.sortBy, state.sortOrder);
                }

                var hasGroupedNameMatches = payload && Array.isArray(payload.NameMatchItems);
                var hasGroupedDescriptionMatches = payload && Array.isArray(payload.DescriptionMatchItems);
                var hasGroupedSearchPayload = hasGroupedNameMatches || hasGroupedDescriptionMatches;

                // Prefer grouped search payload when available (new API contract)
                if (state.searchTerm && hasGroupedSearchPayload) {
                    state.items = hasGroupedNameMatches ? payload.NameMatchItems : [];
                    state.descItems = hasGroupedDescriptionMatches ? payload.DescriptionMatchItems : [];

                    state.nameMatchCount = payload && typeof payload.NameMatchCount === 'number'
                        ? payload.NameMatchCount
                        : state.items.length;
                    state.descMatchCount = payload && typeof payload.DescriptionMatchCount === 'number'
                        ? payload.DescriptionMatchCount
                        : state.descItems.length;
                    state.totalCount = state.nameMatchCount + state.descMatchCount;
                    state.pageIndex = 0;
                }

                // If searching and grouped payload is not available, do secondary description-match
                if (state.searchTerm && !hasGroupedSearchPayload) {
                    state.nameMatchCount = state.items.length;
                    return fetchDescriptionMatches();
                }
            })
            .then(function () {
                renderGrid();
            })
            .catch(function (error) {
                showState('Failed to load data. ' + (error && error.message ? error.message : ''), true);
            })
            .finally(function () {
                state.loading = false;
                renderMeta();
                renderPagination();
                updateRefreshButtonState();
            });
    }

    function fetchDescriptionMatches() {
        // Fetch without searchTerm to get all items, then filter by description client-side
        var params = new URLSearchParams();
        params.set('startIndex', '0');
        params.set('limit', '200');
        params.set('sortBy', state.sortBy);
        params.set('sortOrder', state.sortOrder);
        if (state.userId) {
            params.set('userId', state.userId);
        }
        if (refs && refs.favFilter && refs.favFilter.checked) {
            params.set('isFavorite', 'true');
        }
        if (refs && refs.tagFilter && refs.tagFilter.value) {
            params.set('tag', refs.tagFilter.value);
        }
        if (refs && refs.countryFilter && refs.countryFilter.value) {
            params.set('productionLocation', refs.countryFilter.value);
        }
        if (refs && refs.libraryFilter && refs.libraryFilter.value) {
            params.set('libraryIds', refs.libraryFilter.value);
        }
        var endpoint = '/CastCrew/' + state.activeTab;
        var url = endpoint + '?' + params.toString();

        var fetchFn;
        if (window.ApiClient && typeof window.ApiClient.getJSON === 'function') {
            fetchFn = window.ApiClient.getJSON(url);
        } else {
            fetchFn = fetch(url, {
                method: 'GET',
                headers: buildRequestHeaders()
            }).then(function (response) {
                if (!response.ok) return { Items: [] };
                return response.json();
            });
        }

        var nameMatchIds = {};
        state.items.forEach(function (item) {
            nameMatchIds[item.Id] = true;
        });

        var searchLower = state.searchTerm.toLowerCase();

        return fetchFn.then(function (payload) {
            var allItems = payload && payload.Items ? payload.Items : [];
            state.descItems = allItems.filter(function (person) {
                // Exclude items already in name matches
                if (nameMatchIds[person.Id]) return false;
                // Match against overview/description
                var overview = person.Overview || '';
                return overview.toLowerCase().indexOf(searchLower) !== -1;
            });
            state.descMatchCount = state.descItems.length;
            state.totalCount = (typeof state.nameMatchCount === 'number' ? state.nameMatchCount : state.items.length) + state.descMatchCount;
        }).catch(function () {
            state.descItems = [];
            state.descMatchCount = 0;
            state.totalCount = typeof state.nameMatchCount === 'number' ? state.nameMatchCount : state.items.length;
        });
    }

    function runSearch() {
        if (!refs) {
            return;
        }

        state.searchTerm = refs.searchInput.value.trim();
        state.pageIndex = 0;
        fetchActors();
    }

    function initializeActorsState() {
        if (state.initialized) {
            return true;
        }

        var token = resolveAuthToken();
        if (!token) {
            return false;
        }

        state.authToken = token;
        state.userId = getCurrentUserId();
        state.initialized = true;
        return true;
    }

    function renderActorsHomeMode() {
        var page = findHomePage();
        if (!page) {
            return;
        }

        ensureActorsStyles();
        var host = ensureActorsContainer(page);
        var homeSections = findDefaultHomeSections(page);
        homeSections.forEach(function (section) {
            section.style.display = 'none';
        });

        host.style.display = '';
        hideNativeHomeFavoritesNav();

        if (!initializeActorsState()) {
            if (refs) {
                showState('No active Jellyfin web session found. Sign in and reload this page.', true);
                refs.meta.textContent = '';
            }

            return;
        }

        if (refs) {
            refs.sortSelect.value = buildSortSelection(state.sortBy, state.sortOrder);
        }

        // Issue #3: fetch on first load or if container was recreated (no castcrewLoaded flag)
        if (!host.dataset.castcrewLoaded) {
            host.dataset.castcrewLoaded = 'true';
            fetchActors();
        }
    }

    function restoreDefaultHomeMode() {
        restoreNativeHomeFavoritesNav();

        var page = findHomePage();
        if (page) {
            var homeSections = findDefaultHomeSections(page);
            homeSections.forEach(function (section) {
                section.style.display = '';
            });
        }

        Array.prototype.slice.call(document.querySelectorAll('#' + castCrewContainerId)).forEach(function (host) {
            host.style.display = 'none';
        });
    }

    function syncCastCrewRouteView() {
        if (isActorsRoute()) {
            renderActorsHomeMode();
            return;
        }

        restoreDefaultHomeMode();
    }

    function scheduleEnsure() {
        if (scheduled !== null) {
            window.clearTimeout(scheduled);
        }

        scheduled = window.setTimeout(function () {
            scheduled = null;
            normalizeActorsLinkTargets();
            syncCastCrewRouteView();
        }, 80);
    }

    function throttledScheduleEnsure() {
        var now = Date.now();
        if (now - lastObserverFireTime < 200) {
            return;
        }

        lastObserverFireTime = now;
        scheduleEnsure();
    }

    scheduleEnsure();
    document.addEventListener('DOMContentLoaded', scheduleEnsure, true);
    window.addEventListener('hashchange', scheduleEnsure, true);
    window.addEventListener('popstate', scheduleEnsure, true);
    window.addEventListener('pageshow', scheduleEnsure, true);
    window.addEventListener('load', scheduleEnsure, true);

    var observer = new MutationObserver(throttledScheduleEnsure);
    observer.observe(document.documentElement, { childList: true, subtree: true });
})();
