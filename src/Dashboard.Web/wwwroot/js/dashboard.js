/* Local synthetic dashboard. All captured text is inserted as text, never HTML. */
(function ($) {
    "use strict";
    const app = $('.app'), root = app.attr('data-api-root');
    let section = 'Overview', generation = 0, requests = [], busy = false, paused = false, page = 1, sort = '', direction = 'desc', top = 20, includeExcluded = false, sample = '', context;
    const titles = { Overview: 'Overview', Queries: 'Expensive Queries', Blocking: 'Blocking', Waits: 'Wait Statistics', FileIo: 'Database File I/O' };
    const descriptions = { Overview: 'Understand query activity, blocking, and database performance.', Queries: 'Observed completed query work from persisted cache snapshots.', Blocking: 'Inspect sampled relationships, sleeping roots, and blocked requests.', Waits: 'Understand the composition of instance-level completed waits.', FileIo: 'Inspect per-file throughput and operations-weighted latency.' };
    const label = 'Synthetic data — demonstration only — not connected to SQL Server.';
    const $body = $('#dashboard');
    function number(v, decimals) { return v == null ? '—' : Number(v).toLocaleString('en-US', { maximumFractionDigits: decimals == null ? 2 : decimals }); }
    function time(v) { return v ? new Date(v).toISOString().replace('T', ' ').replace('.000Z', ' UTC') : 'Unavailable'; }
    function state(s) { return String(s || 'unavailable').replace(/([a-z])([A-Z])/g, '$1 $2'); }
    function badge(s) { return $('<span>').addClass('state').addClass(/^(failure|reset|gap|invalid|partial)$/.test(s) ? s : '').text(state(s)); }
    function api(action, params) { const xhr = $.ajax({ url: root + action, data: params, dataType: 'json', timeout: 10000 }); requests.push(xhr); return xhr; }
    function common() {
        const q = { instanceId: $('#instance').val(), sourceMode: 'synthetic', scenarioId: $('#scenario').val(), range: $('#range').val() };
        if ($('#database').val()) q.databaseId = $('#database').val();
        if (q.range === 'custom') {
            q.fromUtc = utcInput($('#fromUtc').val()); q.toUtc = utcInput($('#toUtc').val());
        }
        return q;
    }
    function utcInput(value) { return value ? value + (value.length === 16 ? ':00' : '') + 'Z' : ''; }
    function panel(title, note) {
        const p = $('<div>').addClass('panel');
        p.append($('<div>').addClass('panel-head').append($('<h2>').text(title), $('<span>').addClass('tag').text('SYNTHETIC DATA')));
        if (note) p.append($('<div>').addClass('panel-body').append($('<p>').addClass('panel-note').text(note)));
        return p;
    }
    function table(headers, rows, empty) {
        const t = $('<table>').append($('<caption>').text(label));
        const head = $('<tr>'); headers.forEach(h => head.append($('<th>').attr('scope', 'col').text(h)));
        t.append($('<thead>').append(head)); const body = $('<tbody>');
        rows.forEach(row => { const tr = $('<tr>'); row.forEach((cell, index) => { const td = $('<td>').text(cell == null ? '—' : cell); if (index > 1) td.addClass('number'); if (headers[index] === 'Statement summary') td.removeClass('number').addClass('summary-cell'); tr.append(td); }); body.append(tr); });
        if (!rows.length) body.append($('<tr>').append($('<td>').attr('colspan', headers.length).addClass('empty').text(empty || 'No observations in the covered intervals.')));
        t.append(body); return $('<div>').addClass('table-wrap').append(t);
    }
    function coverage(c) {
        if (!c) return;
        const rejects = Object.entries(c.rejectedReasons || {}).map(([reason, count]) => state(reason) + ': ' + count).join(' · ');
        return $('<div>').addClass('panel-body').append($('<p>').addClass('panel-note').text('Measured coverage: ' + number(c.coveredSeconds) + ' seconds (' + number(c.coveragePct) + '% of selected range). ' + (rejects || 'Only complete compatible intervals contribute.')));
    }
    function pager(p) {
        const container = $('<div>').addClass('pager').append($('<span>').text(p.totalEligibleRows + ' eligible rows · Page ' + p.page));
        const prev = $('<button>').addClass('button').text('Previous').prop('disabled', p.page <= 1).on('click', () => { page--; refresh(); });
        const next = $('<button>').addClass('button').text('Next').prop('disabled', !p.hasMore).on('click', () => { page++; refresh(); });
        return container.append($('<div>').append(prev, next));
    }
    function sortControl(options, current) {
        const select = $('<select>').attr('aria-label', 'Sort metric');
        options.forEach(([value, text]) => select.append($('<option>').val(value).text(text)));
        select.val(sort || current).on('change', function () { sort = this.value; page = 1; refresh(); });
        const order = $('<select>').attr('aria-label', 'Sort direction').append($('<option>').val('desc').text('Descending'), $('<option>').val('asc').text('Ascending')).val(direction).on('change', function () { direction = this.value; page = 1; refresh(); });
        return $('<div>').addClass('toolbar').append($('<label>').text('Sort by').append(select), order);
    }
    function renderOverview(response, history) {
        const d = response.data, cards = $('<div>').addClass('cards');
        const labels = { batchRequestsPerSec: ['Batch throughput', 'batches/s', '↗'], processMemoryMiB: ['Process memory', 'MiB', '▧'], uptimeSeconds: ['SQL Server uptime', 'days', '◷'], userSessions: ['User sessions', 'sessions', '♙'], activeRequests: ['Active requests', 'requests', '≋'], blockedRequests: ['Blocked requests', 'requests', '⑂'], readLatencyMs: ['Read latency', 'ms/read', '↓'], writeLatencyMs: ['Write latency', 'ms/write', '↑'] };
        const order = ['userSessions', 'activeRequests', 'blockedRequests', 'batchRequestsPerSec', 'processMemoryMiB', 'readLatencyMs', 'writeLatencyMs', 'uptimeSeconds'];
        order.forEach(key => {
            const m = d.metrics.find(x => x.key === key), info = labels[key]; if (!m) return;
            const clickable = key === 'blockedRequests' || key === 'readLatencyMs' || key === 'writeLatencyMs';
            const card = $(clickable ? '<button>' : '<div>').addClass('card').toggleClass('metric-link', clickable);
            if (clickable) card.attr('type', 'button').on('click', () => navigate(key === 'blockedRequests' ? 'Blocking' : 'FileIo'));
            const value = m.value == null ? state(m.state) : number(key === 'uptimeSeconds' ? m.value / 86400 : m.value);
            card.append($('<div>').addClass('card-label').append($('<span>').text(info[0]), $('<span>').addClass('card-icon').text(info[2])), $('<div>').addClass('card-value').text(value).append(m.value == null ? '' : $('<small>').text(info[1])), $('<div>').addClass('card-note').text((m.scope === 'instance' ? 'Instance-wide' : 'Database scope') + ' · Synthetic data'));
            card.attr('title', 'Capture: ' + time(m.capturedAtUtc) + (m.measuredSeconds ? ' · Measured interval: ' + m.measuredSeconds + ' seconds' : '') + (m.reason ? ' · ' + state(m.reason) : ''));
            cards.append(card);
        });
        $body.append(cards);
        const grid = $('<div>').addClass('section-grid');
        const trend = panel('Batch throughput', 'Synthetic interval observations. Gaps and resets remain disconnected.');
        const trendBody = $('<div>').addClass('panel-body');
        const points = history && history.data ? history.data.points : [];
        trendBody.append(chart(points), $('<div>').addClass('chart-caption').append($('<span>').text('Batches / second'), $('<span>').text('Actual measured intervals · UTC')));
        const details = $('<details>').append($('<summary>').text('View chart data table'), table(['Capture UTC', 'Value (batches/s)', 'State'], points.map(p => [time(p.endUtc), number(p.value), state(p.state)])));
        trendBody.append(details); trend.append(trendBody); grid.append(trend);
        const health = panel('Collection status', 'Collector observations, not a statement that SQL Server is healthy.');
        const list = $('<ul>').addClass('collection-list'); response.sections.forEach(s => list.append($('<li>').append($('<div>').text(s.section === 'fileIo' ? 'File I/O' : s.section.charAt(0).toUpperCase() + s.section.slice(1)).append($('<div>').addClass('sample-time').text(time(s.lastSuccessUtc))), badge(s.attemptStatus))));
        health.append($('<div>').addClass('panel-body').append(list)); grid.append(health); $body.append(grid);
        const q = panel('Snapshot interpretation', 'Frozen synthetic scenario. Refresh reloads persisted fixtures; it does not generate new activity.');
        q.append($('<div>').addClass('panel-body').append($('<p>').addClass('panel-note').text('Choose Blocking chain, High query reads, or File I/O slowdown above to explore the same dashboard with different scenarios. Memory, uptime, batches and waits remain instance-wide when a database is selected.'))); $body.append(q);
    }
    function chart(points) {
        const ns = 'http://www.w3.org/2000/svg'; const svg = document.createElementNS(ns, 'svg'); svg.setAttribute('viewBox', '0 0 650 180'); svg.setAttribute('class', 'chart'); svg.setAttribute('role', 'img'); svg.setAttribute('aria-label', 'Synthetic batch throughput; tabular equivalent below');
        function element(tag, attrs, text) { const e = document.createElementNS(ns, tag); Object.entries(attrs).forEach(([k, v]) => e.setAttribute(k, v)); if (text != null) e.textContent = text; svg.appendChild(e); return e; }
        const max = Math.max(1, ...points.filter(p => p.value != null).map(p => p.value)) * 1.2;
        for (let i = 0; i < 4; i++) { const y = 145 - i * 40; element('line', { x1: 43, x2: 630, y1: y, y2: y, class: 'axis' }); element('text', { x: 0, y: y + 4 }, number(max * i / 3, 0)); }
        let prior;
        points.forEach((p, index) => {
            const x = points.length === 1 ? 335 : 50 + index * 570 / Math.max(1, points.length - 1); if (p.value == null) { prior = null; return; }
            const y = 145 - p.value / max * 120;
            if (prior) element('line', { x1: prior.x, y1: prior.y, x2: x, y2: y, class: 'trend' });
            const dot = element('circle', { cx: x, cy: y, r: 4, class: 'point' }); const title = document.createElementNS(ns, 'title'); title.textContent = time(p.endUtc) + ': ' + number(p.value) + ' batches/s · Synthetic data'; dot.appendChild(title);
            element('text', { x: Math.max(45, Math.min(550, x - 30)), y: 172 }, new Date(p.endUtc).toISOString().slice(11, 19)); prior = { x, y };
        });
        return svg;
    }
    function renderQueries(d) {
        const p = panel('Observed completed query work', 'Cached-plan observations are not a complete workload audit. Rankings use interval deltas, not lifetime totals.');
        const toolbar = sortControl([['totalCpuMs', 'Total CPU'], ['totalElapsedMs', 'Total elapsed'], ['logicalReads', 'Logical reads'], ['executions', 'Executions'], ['avgCpuMs', 'Average CPU'], ['avgElapsedMs', 'Average elapsed']], 'totalCpuMs');
        const topSelect = $('<select>').attr('aria-label', 'Top queries').append($('<option>').val('20').text('Top 20'), $('<option>').val('50').text('Top 50')).val(String(top)).on('change', function () { top = Number(this.value); page = 1; refresh(); }); toolbar.append(topSelect); p.append(toolbar);
        const list = d.queries;
        p.append(table(['Database', 'Statement summary', 'Executions', 'CPU ms', 'Avg CPU ms', 'Elapsed ms', 'Avg elapsed ms', 'Logical reads', 'Coverage'], list.rows.map(r => [r.database, r.summary || state(r.textState), number(r.executions, 0), number(r.totalCpuMs), number(r.avgCpuMs), number(r.totalElapsedMs), number(r.avgElapsedMs), number(r.logicalReads, 0), state(r.coverageState)]), d.state === 'noActivity' ? 'No observed completed query work in the covered intervals.' : state(d.state)), coverage(list.coverage), pager(list)); $body.append(p);
    }
    function renderWaits(d) {
        const p = panel('Instance-level completed waits', 'Wait shares express composition, not CPU utilization or wall time. Database filters do not apply.');
        const toolbar = sortControl([['totalWaitMs', 'Total wait'], ['signalWaitMs', 'Signal wait'], ['resourceWaitMs', 'Resource wait'], ['taskCount', 'Task count'], ['sharePct', 'Share']], 'totalWaitMs');
        toolbar.append($('<label>').append($('<input>').attr('type', 'checkbox').prop('checked', includeExcluded).on('change', function () { includeExcluded = this.checked; page = 1; refresh(); }), document.createTextNode(' Include excluded waits'))); p.append(toolbar);
        p.append(table(['Wait type', 'Total wait ms', 'Signal ms', 'Resource ms', 'Task count', 'Share of included waits'], d.waits.rows.map(r => [r.waitType, number(r.totalWaitMs), number(r.signalWaitMs), number(r.resourceWaitMs), number(r.taskCount, 0), r.sharePct == null ? 'No observed waits' : number(r.sharePct) + '%']), 'No observed completed waits.'));
        p.append(coverage(d.waits.coverage), pager(d.waits));
        p.append($('<div>').addClass('panel-body').append($('<details>').append($('<summary>').text('Exclusion policy: ' + d.exclusionPolicyVersion), $('<p>').text(d.excludedTypes.join(', '))))); $body.append(p);
    }
    function renderFiles(d) {
        const strip = $('<div>').addClass('stat-strip'); [['Read latency', d.summary.readLatencyMs, 'ms/read'], ['Write latency', d.summary.writeLatencyMs, 'ms/write'], ['Read throughput', d.summary.readMiBps, 'MiB/s'], ['Write throughput', d.summary.writeMiBps, 'MiB/s']].forEach(([name, value, unit]) => strip.append($('<div>').text(name).append($('<strong>').text(value == null ? 'No I/O / unavailable' : number(value) + ' ' + unit)))); $body.append(strip);
        const p = panel('Database files', 'Data and log files are separate. Summary latency is weighted by operations across complete compatible file intervals.');
        p.append(sortControl([['database', 'Database'], ['fileName', 'File name'], ['fileType', 'File type'], ['readIops', 'Read IOPS'], ['writeIops', 'Write IOPS'], ['readLatencyMs', 'Read latency'], ['writeLatencyMs', 'Write latency']], 'database'));
        p.append(table(['Database', 'File', 'Type', 'Read IOPS', 'Write IOPS', 'Read MiB/s', 'Write MiB/s', 'Read ms/op', 'Write ms/op', 'Interval s'], d.files.rows.map(r => [r.database, r.fileName, r.fileType, number(r.metrics.readIops), number(r.metrics.writeIops), number(r.metrics.readMiBps), number(r.metrics.writeMiBps), r.metrics.readLatencyMs == null ? state(r.readLatencyState) : number(r.metrics.readLatencyMs), r.metrics.writeLatencyMs == null ? state(r.writeLatencyState) : number(r.metrics.writeLatencyMs), number(r.metrics.seconds)])), coverage(d.files.coverage), pager(d.files)); $body.append(p);
    }
    function renderBlocking(d, samples) {
        const strip = $('<div>').addClass('stat-strip'); [['Blocked requests', d.fullSampleBlockedRequestCount], ['Root blockers', d.rootCount], ['Special owners', d.specialOwnerCount]].forEach(([name, value]) => strip.append($('<div>').text(name).append($('<strong>').text(number(value, 0))))); $body.append(strip);
        const p = panel('Sampled blocking chain', 'Sleeping roots are included. Current wait duration is separate from observed relationship timestamps.');
        const picker = $('<select>').attr('aria-label', 'Blocking sample').append($('<option>').val('').text('Latest successful sample'));
        (samples && samples.data ? samples.data.rows : []).filter(x => x.status === 'success').forEach(s => picker.append($('<option>').val(s.sampleId).text(time(s.captureUtc)))); picker.val(sample).on('change', function () { sample = this.value; refresh(); });
        p.append($('<div>').addClass('toolbar').append($('<label>').text('Capture').append(picker), $('<span>').text((d.historical ? 'Historical sample · ' : 'Frozen synthetic sample · ') + time(d.sampleUtc))));
        const chain = $('<ul>').addClass('chain'); const nodes = new Map(d.nodes.map(n => [n.nodeId, n])); const visited = new Set();
        function node(id, depth) {
            if (visited.has(id)) { chain.append($('<li>').addClass('child').text('Cycle or shared node: ' + id)); return; } visited.add(id);
            const n = nodes.get(id); if (!n) return;
            const li = $('<li>').addClass(depth ? 'child' : 'root').append($('<strong>').text((n.sessionId == null ? 'Special owner' : 'Session ' + n.sessionId) + (n.requestId == null ? '' : ' / Request ' + n.requestId) + ' · ' + n.status), $('<p>').text(n.database + (n.contextOnly ? ' · Cross-database context' : '') + ' · Open transactions: ' + n.openTransactions + (n.currentWaitType ? ' · ' + n.currentWaitType + ' · Current wait ' + number(n.currentWaitMs) + ' ms' : ''))); chain.append(li);
            d.edges.filter(e => e.parentId === id).forEach(e => node(e.childId, depth + 1));
        }
        d.roots.forEach(id => node(id, 0));
        p.append($('<div>').addClass('panel-body').append(d.nodes.length ? chain : $('<div>').addClass('empty').text('No positive-owner blocked requests observed at this capture.')));
        p.append(table(['Session / request', 'Database', 'Status', 'Wait type', 'Current wait ms', 'Elapsed ms', 'Transactions'], d.nodes.map(n => [(n.sessionId == null ? n.nodeId : n.sessionId) + (n.requestId == null ? '' : ' / ' + n.requestId), n.database, n.status, n.currentWaitType || '—', number(n.currentWaitMs), number(n.elapsedRequestMs), n.openTransactions]))); $body.append(p);
    }
    function navigate(next) { section = next; page = 1; sort = ''; sample = ''; $('.nav-item').removeClass('selected').removeAttr('aria-current'); $('.nav-item[data-section="' + next + '"]').addClass('selected').attr('aria-current', 'page'); $('#page-title').text(titles[next]); $('#page-subtitle').text(descriptions[next]); refresh(); }
    async function refresh() {
        generation++; const token = generation; requests.forEach(x => x.abort()); requests = []; busy = true;
        $body.attr('aria-busy', 'true'); $('#error').prop('hidden', true); $('#notice').prop('hidden', true);
        try {
            const q = common();
            const c = await api('Context', q); if (token !== generation) return; context = c.context;
            const selectedDb = $('#database').val(); const select = $('#database').empty().append($('<option>').val('').text('All databases'));
            c.data.databases.forEach(db => select.append($('<option>').val(db.databaseId).text(db.name))); select.val(selectedDb); if (select.val() == null) select.val('');
            if (q.databaseId && select.val() !== q.databaseId) { delete q.databaseId; }
            q.asOfSampleId = context.asOfSampleId;
            $('#capture-context').text('Frozen synthetic scenario · ' + context.scenarioId + ' · Evaluation ' + time(context.scenarioEvaluationUtc));
            const params = Object.assign({}, q);
            if (section !== 'Overview') { params.page = page; params.pageSize = 20; params.direction = direction; if (sort) params.sort = sort; }
            if (section === 'Queries') params.top = top;
            if (section === 'Waits') params.includeExcluded = includeExcluded;
            if (section === 'Blocking' && sample) params.sampleId = sample;
            const extras = section === 'Overview' ? api('History', Object.assign({}, q, { metric: 'batchRequestsPerSec' })) : section === 'Blocking' ? api('Samples', q) : Promise.resolve(null);
            const results = await Promise.all([api(section, params), extras]); if (token !== generation) return;
            $body.empty(); const response = results[0];
            if (section === 'Overview') renderOverview(response, results[1]); else if (section === 'Queries') renderQueries(response.data); else if (section === 'Waits') renderWaits(response.data); else if (section === 'FileIo') renderFiles(response.data); else renderBlocking(response.data, results[1]);
            const failures = response.sections.filter(s => s.attemptStatus !== 'success');
            if (failures.length) $('#notice').text('Partial collection: ' + failures.map(s => s.section + ' — ' + state(s.reason || s.attemptStatus) + '. Last success: ' + time(s.lastSuccessUtc)).join(' · ')).prop('hidden', false);
        } catch (err) {
            if (token !== generation || err.statusText === 'abort') return;
            const message = err.responseJSON && err.responseJSON.error ? err.responseJSON.error.message : 'Unable to load snapshots. Check the local application and database, then refresh.';
            $('#error').text(message).prop('hidden', false); if (!$body.find('.panel,.cards').length) $body.empty().append($('<div>').addClass('empty').text('Monitoring data is unavailable.'));
        } finally { if (token === generation) { busy = false; $body.attr('aria-busy', 'false'); requests = []; } }
    }
    $('.nav-item').on('click', function () { navigate($(this).attr('data-section')); });
    $('#filters').on('submit', e => { e.preventDefault(); refresh(); });
    $('#scenario').on('change', () => { page = 1; sample = ''; $('#database').val(''); $body.empty().append($('<div>').addClass('loading').text('Loading scenario…')); refresh(); });
    $('#database').on('change', () => { page = 1; sample = ''; $body.empty(); refresh(); });
    $('#range').on('change', function () { $('#custom-range').prop('hidden', this.value !== 'custom'); page = 1; if (this.value !== 'custom') refresh(); else if (context) { $('#fromUtc').val(context.fromUtc.slice(0, 19)); $('#toUtc').val(context.toUtc.slice(0, 19)); } });
    $('#pause').on('click', function () { paused = !paused; $(this).text(paused ? 'Resume' : 'Pause').attr('aria-pressed', String(paused)); $('#refresh-state').text(paused ? 'Refresh paused · Frozen scenario · UTC' : 'Auto-refresh · 15 seconds · UTC'); if (!paused && !busy) refresh(); });
    window.setInterval(() => { if (!paused && !busy && !document.hidden) refresh(); }, 15000);
    refresh();
})(jQuery);
