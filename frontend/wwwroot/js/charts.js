// wwwroot/js/charts.js
// Minimal Chart.js wrapper for ReportChart.razor.
// Chart.js is loaded via CDN in index.html:
//   <script src="https://cdn.jsdelivr.net/npm/chart.js@4/dist/chart.umd.min.js"></script>

window.lidstroem = window.lidstroem || {};

// Keep a registry of active chart instances so we can destroy
// before re-rendering (Blazor may call renderChart multiple times).
const _chartInstances = new WeakMap();

window.lidstroem.renderChart = function (canvas, opts) {
    if (!canvas || !window.Chart) return;

    // Destroy any previous instance on this canvas
    const existing = _chartInstances.get(canvas);
    if (existing) {
        existing.destroy();
        _chartInstances.delete(canvas);
    }

    // Read skin CSS custom properties so the chart inherits tenant branding
    const style = getComputedStyle(document.documentElement);
    const primary   = style.getPropertyValue('--color-primary').trim()   || '#6366f1';
    const surface   = style.getPropertyValue('--color-surface').trim()   || '#ffffff';
    const textColor = style.getPropertyValue('--color-text').trim()       || '#111827';
    const border    = style.getPropertyValue('--color-border').trim()     || '#e5e7eb';

    // Generate a palette derived from the primary colour for multi-series charts
    const palette = buildPalette(primary, opts.labels?.length ?? 1);

    const chartType = opts.type || 'bar';
    const isPie     = chartType === 'pie' || chartType === 'doughnut';

    const chart = new Chart(canvas, {
        type: chartType,
        data: {
            labels: opts.labels || [],
            datasets: [{
                label: opts.label || '',
                data: opts.values || [],
                backgroundColor: isPie ? palette : primary + 'cc',
                borderColor:     isPie ? palette.map(c => c) : primary,
                borderWidth: isPie ? 2 : 1,
                borderRadius: chartType === 'bar' ? 4 : 0,
                tension: 0.4
            }]
        },
        options: {
            responsive: true,
            maintainAspectRatio: false,
            plugins: {
                legend: {
                    display: isPie,
                    labels: { color: textColor, font: { size: 12 } }
                },
                tooltip: {
                    callbacks: {
                        label: ctx => {
                            const v = ctx.parsed.y ?? ctx.parsed;
                            return typeof v === 'number'
                                ? ` ${v.toLocaleString()}`
                                : ` ${v}`;
                        }
                    }
                }
            },
            scales: isPie ? {} : {
                x: {
                    ticks: { color: textColor },
                    grid:  { color: border }
                },
                y: {
                    beginAtZero: true,
                    ticks: {
                        color: textColor,
                        callback: v => typeof v === 'number' ? v.toLocaleString() : v
                    },
                    grid: { color: border }
                }
            }
        }
    });

    _chartInstances.set(canvas, chart);
};

// Build an array of colours by rotating hue around the primary
function buildPalette(hex, count) {
    const [h, s, l] = hexToHsl(hex);
    const result = [];
    for (let i = 0; i < count; i++) {
        const hue = (h + i * (360 / count)) % 360;
        result.push(`hsl(${hue}, ${s}%, ${l}%)`);
    }
    return result;
}

function hexToHsl(hex) {
    hex = hex.replace('#', '');
    if (hex.length === 3)
        hex = hex.split('').map(c => c + c).join('');
    const r = parseInt(hex.slice(0, 2), 16) / 255;
    const g = parseInt(hex.slice(2, 4), 16) / 255;
    const b = parseInt(hex.slice(4, 6), 16) / 255;
    const max = Math.max(r, g, b), min = Math.min(r, g, b);
    let h = 0, s = 0;
    const l = (max + min) / 2;
    if (max !== min) {
        const d = max - min;
        s = l > 0.5 ? d / (2 - max - min) : d / (max + min);
        switch (max) {
            case r: h = ((g - b) / d + (g < b ? 6 : 0)) / 6; break;
            case g: h = ((b - r) / d + 2) / 6; break;
            case b: h = ((r - g) / d + 4) / 6; break;
        }
    }
    return [Math.round(h * 360), Math.round(s * 100), Math.round(l * 100)];
}
