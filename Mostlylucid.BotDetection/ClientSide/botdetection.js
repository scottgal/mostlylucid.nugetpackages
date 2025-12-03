/**
 * Mostlylucid.BotDetection - Browser Fingerprinting & Headless Detection
 *
 * This script collects minimal, non-invasive browser signals to detect
 * headless browsers and automation frameworks. Results are posted to
 * a server endpoint for correlation with request-based detection.
 *
 * Signals collected:
 * - Basic: timezone, language, platform, screen, hardware
 * - Automation markers: webdriver, phantom, selenium, CDP
 * - Consistency: window dimensions, function integrity
 * - Optional: WebGL vendor/renderer, canvas hash
 *
 * Privacy notes:
 * - No cookies or localStorage used for tracking
 * - Fingerprint hash is ephemeral (session-scoped)
 * - No PII collected
 */
(function() {
    'use strict';

    // Configuration injected by TagHelper
    // %%CONFIG%% will be replaced with actual values
    var MLBotD = {
        version: '%%VERSION%%',
        token: '%%TOKEN%%',
        endpoint: '%%ENDPOINT%%',
        config: {
            collectWebGL: %%COLLECT_WEBGL%%,
            collectCanvas: %%COLLECT_CANVAS%%,
            collectAudio: %%COLLECT_AUDIO%%,
            timeout: %%TIMEOUT%%
        },

        /**
         * Simple non-cryptographic hash for fingerprint components
         */
        hash: function(str) {
            var hash = 0;
            for (var i = 0; i < str.length; i++) {
                hash = ((hash << 5) - hash) + str.charCodeAt(i);
                hash |= 0; // Convert to 32-bit integer
            }
            return hash.toString(16);
        },

        /**
         * Collect browser fingerprint signals
         */
        collect: function() {
            var data = {};
            var nav = navigator;
            var win = window;
            var scr = screen;

            // ===== Basic Signals (low entropy, non-invasive) =====
            data.tz = this.getTimezone();
            data.lang = nav.language || '';
            data.langs = (nav.languages || []).slice(0, 3).join(',');
            data.platform = nav.platform || '';
            data.cores = nav.hardwareConcurrency || 0;
            data.mem = nav.deviceMemory || 0;
            data.touch = 'ontouchstart' in win ? 1 : 0;
            data.screen = scr.width + 'x' + scr.height + 'x' + scr.colorDepth;
            data.avail = scr.availWidth + 'x' + scr.availHeight;
            data.dpr = win.devicePixelRatio || 1;
            data.pdf = this.hasPdfPlugin() ? 1 : 0;

            // ===== Headless/Automation Detection =====
            data.webdriver = nav.webdriver ? 1 : 0;
            data.phantom = this.detectPhantom();
            data.nightmare = !!win.__nightmare ? 1 : 0;
            data.selenium = this.detectSelenium();
            data.cdc = this.detectCDP();
            data.plugins = nav.plugins ? nav.plugins.length : 0;
            data.chrome = !!win.chrome ? 1 : 0;
            data.permissions = this.checkPermissions();

            // ===== Window Consistency =====
            data.outerW = win.outerWidth || 0;
            data.outerH = win.outerHeight || 0;
            data.innerW = win.innerWidth || 0;
            data.innerH = win.innerHeight || 0;

            // ===== Function Integrity =====
            data.evalLen = this.getEvalLength();
            data.bindNative = this.isBindNative() ? 1 : 0;

            // ===== Optional: WebGL =====
            if (this.config.collectWebGL) {
                var gl = this.getWebGLInfo();
                if (gl) {
                    data.glVendor = gl.vendor || '';
                    data.glRenderer = gl.renderer || '';
                }
            }

            // ===== Optional: Canvas Hash =====
            if (this.config.collectCanvas) {
                data.canvasHash = this.getCanvasHash();
            }

            // ===== Client-side Score =====
            data.score = this.calculateScore(data);

            return data;
        },

        /**
         * Get timezone safely
         */
        getTimezone: function() {
            try {
                return Intl.DateTimeFormat().resolvedOptions().timeZone || '';
            } catch (e) {
                return '';
            }
        },

        /**
         * Check for PDF plugin
         */
        hasPdfPlugin: function() {
            try {
                var plugins = navigator.plugins;
                for (var i = 0; i < plugins.length; i++) {
                    if (plugins[i].name.toLowerCase().indexOf('pdf') > -1) {
                        return true;
                    }
                }
            } catch (e) {}
            return false;
        },

        /**
         * Detect PhantomJS markers
         */
        detectPhantom: function() {
            return (window.phantom || window._phantom || window.callPhantom) ? 1 : 0;
        },

        /**
         * Detect Selenium markers
         */
        detectSelenium: function() {
            var doc = document;
            return (doc.__selenium_unwrapped ||
                    doc.__webdriver_evaluate ||
                    doc.__driver_evaluate ||
                    doc.__webdriver_script_function ||
                    doc.__webdriver_script_func ||
                    doc.__webdriver_script_fn ||
                    doc.$cdc_asdjflasutopfhvcZLmcfl_ ||
                    doc.$chrome_asyncScriptInfo) ? 1 : 0;
        },

        /**
         * Detect Chrome DevTools Protocol markers (Puppeteer, Playwright)
         */
        detectCDP: function() {
            try {
                for (var key in window) {
                    if (key.match(/^cdc_|^__\$|^\$cdc_/)) {
                        return 1;
                    }
                }
            } catch (e) {}
            return 0;
        },

        /**
         * Check notification permissions for consistency
         */
        checkPermissions: function() {
            try {
                if (typeof Notification === 'undefined') {
                    return 'unavailable';
                }
                // Suspicious: denied permissions with no plugins (common in headless)
                if (Notification.permission === 'denied' && navigator.plugins.length === 0) {
                    return 'suspicious';
                }
                return Notification.permission;
            } catch (e) {
                return 'error';
            }
        },

        /**
         * Get eval function length (modified in some automation tools)
         */
        getEvalLength: function() {
            try {
                return eval.toString().length;
            } catch (e) {
                return 0;
            }
        },

        /**
         * Check if Function.prototype.bind is native
         */
        isBindNative: function() {
            try {
                return Function.prototype.bind.toString().indexOf('[native code]') > -1;
            } catch (e) {
                return false;
            }
        },

        /**
         * Get WebGL vendor and renderer info
         */
        getWebGLInfo: function() {
            try {
                var canvas = document.createElement('canvas');
                var gl = canvas.getContext('webgl') || canvas.getContext('experimental-webgl');
                if (!gl) return null;

                var debugInfo = gl.getExtension('WEBGL_debug_renderer_info');
                if (!debugInfo) return { vendor: '', renderer: '' };

                return {
                    vendor: gl.getParameter(debugInfo.UNMASKED_VENDOR_WEBGL) || '',
                    renderer: gl.getParameter(debugInfo.UNMASKED_RENDERER_WEBGL) || ''
                };
            } catch (e) {
                return null;
            }
        },

        /**
         * Generate a simple canvas hash for consistency checking
         */
        getCanvasHash: function() {
            try {
                var canvas = document.createElement('canvas');
                canvas.width = 200;
                canvas.height = 50;
                var ctx = canvas.getContext('2d');

                // Draw some elements that will vary by GPU/driver
                ctx.textBaseline = 'top';
                ctx.font = '14px Arial';
                ctx.fillStyle = '#f60';
                ctx.fillRect(125, 1, 62, 20);
                ctx.fillStyle = '#069';
                ctx.fillText('MLBotD', 2, 15);
                ctx.fillStyle = 'rgba(102, 204, 0, 0.7)';
                ctx.fillText('MLBotD', 4, 17);

                return this.hash(canvas.toDataURL());
            } catch (e) {
                return '';
            }
        },

        /**
         * Calculate client-side integrity score
         */
        calculateScore: function(data) {
            var score = 100;

            // Definite automation markers
            if (data.webdriver) score -= 50;
            if (data.phantom) score -= 50;
            if (data.nightmare) score -= 50;
            if (data.selenium) score -= 50;
            if (data.cdc) score -= 40;

            // Suspicious indicators
            if (data.plugins === 0 && data.chrome) score -= 20; // Chrome should have plugins
            if (data.outerW === 0 || data.outerH === 0) score -= 30; // Headless often has 0 outer
            if (data.innerW === data.outerW && data.innerH === data.outerH) score -= 10; // No browser chrome
            if (!data.bindNative) score -= 20; // Prototype pollution
            if (data.evalLen > 0 && (data.evalLen < 30 || data.evalLen > 50)) score -= 15; // Modified eval
            if (data.permissions === 'suspicious') score -= 25;

            return Math.max(0, score);
        },

        /**
         * Send fingerprint data to server
         */
        send: function(data) {
            var self = this;
            var xhr = new XMLHttpRequest();
            xhr.open('POST', this.endpoint, true);
            xhr.setRequestHeader('Content-Type', 'application/json');
            xhr.setRequestHeader('X-ML-BotD-Token', this.token);
            xhr.timeout = this.config.timeout;

            xhr.onerror = function() {
                // Silent fail - don't break the page
            };

            xhr.send(JSON.stringify(data));
        },

        /**
         * Main entry point
         */
        run: function() {
            var self = this;

            // Small delay to not block page load
            setTimeout(function() {
                try {
                    var data = self.collect();
                    data.ts = Date.now();
                    self.send(data);
                } catch (e) {
                    // Send error report
                    self.send({
                        error: e.message || 'Unknown error',
                        ts: Date.now()
                    });
                }
            }, 100);
        }
    };

    // Run when DOM is ready
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', function() {
            MLBotD.run();
        });
    } else {
        MLBotD.run();
    }
})();
