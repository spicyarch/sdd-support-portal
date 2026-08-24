(function () {
    "use strict";

    var defaultProductName = "Support Portal";

    function isSafeText(value, maximumLength) {
        return typeof value === "string" && value.trim().length > 0 && value.trim().length <= maximumLength && !/[\r\n]/.test(value);
    }

    function isSafeImageUrl(value) {
        if (value === null || value === undefined || value === "") {
            return true;
        }

        try {
            var url = new URL(value);
            return (url.protocol === "https:" ||
                (url.protocol === "http:" && (url.hostname === "localhost" || url.hostname === "127.0.0.1"))) &&
                url.username === "" &&
                url.password === "" &&
                url.hash === "";
        } catch (_) {
            return false;
        }
    }

    function applyBrand(profile) {
        if (!isSafeText(profile.productName, 100) || !isSafeImageUrl(profile.faviconUrl)) {
            return;
        }

        document.title = profile.productName.trim();
        var favicon = document.querySelector("link[rel='icon']");
        if (!favicon) {
            favicon = document.createElement("link");
            favicon.rel = "icon";
            document.head.appendChild(favicon);
        }

        favicon.onerror = function () {
            favicon.onerror = null;
            favicon.href = "favicon.png";
        };
        favicon.href = profile.faviconUrl || "favicon.png";
    }

    async function loadBrand() {
        document.title = defaultProductName;
        try {
            var settingsResponse = await fetch("appsettings.json", { cache: "no-store" });
            if (!settingsResponse.ok) {
                return;
            }

            var settings = await settingsResponse.json();
            var configuredBaseUrl = settings && settings.Api && settings.Api.BaseUrl;
            if (typeof configuredBaseUrl !== "string" || configuredBaseUrl.trim() === "") {
                return;
            }

            var brandingResponse = await fetch(configuredBaseUrl.replace(/\/+$/, "") + "/branding", {
                headers: { Accept: "application/json" },
                cache: "no-store"
            });
            if (brandingResponse.ok) {
                applyBrand(await brandingResponse.json());
            }
        } catch (_) {
            // The Blazor BrandingState remains the authoritative fallback.
        }
    }

    void loadBrand();
})();
