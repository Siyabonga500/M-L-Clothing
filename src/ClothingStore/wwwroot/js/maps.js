// Map helpers built on Leaflet + OpenStreetMap (free, no API key).
(function () {
    const TILE_URL = 'https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png';
    const TILE_ATTR = '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a>';
    const NOMINATIM = 'https://nominatim.openstreetmap.org';

    function pinIcon(color, glyph) {
        return L.divIcon({
            className: '',
            html: `<div style="background:${color};width:34px;height:34px;border-radius:50% 50% 50% 0;transform:rotate(-45deg);
                   display:flex;align-items:center;justify-content:center;box-shadow:0 2px 6px rgba(0,0,0,.35);border:2px solid #fff">
                   <i class="bi ${glyph}" style="transform:rotate(45deg);color:#fff;font-size:15px"></i></div>`,
            iconSize: [34, 34], iconAnchor: [17, 34], popupAnchor: [0, -30]
        });
    }
    const icons = {
        store: () => pinIcon('#023436', 'bi-shop'),
        home: () => pinIcon('#c8473b', 'bi-house-door-fill'),
        driver: () => pinIcon('#e0a93b', 'bi-truck')
    };

    function baseMap(el, lat, lng, zoom) {
        const map = L.map(el).setView([lat, lng], zoom);
        L.tileLayer(TILE_URL, { maxZoom: 19, attribution: TILE_ATTR }).addTo(map);
        return map;
    }

    // Same estimate as the server (Geo.Estimate): straight line x road factor, average speed, +5 min hand-over.
    function estimate(lat1, lon1, lat2, lon2) {
        const R = 6371, rad = d => d * Math.PI / 180;
        const dLat = rad(lat2 - lat1), dLon = rad(lon2 - lon1);
        const a = Math.sin(dLat / 2) ** 2 + Math.cos(rad(lat1)) * Math.cos(rad(lat2)) * Math.sin(dLon / 2) ** 2;
        const km = R * 2 * Math.atan2(Math.sqrt(a), Math.sqrt(1 - a)) * 1.35;
        const minutes = Math.ceil(km / Math.max(5, STORE.speedKmh) * 60) + 5;
        return { km: Math.round(km * 10) / 10, minutes };
    }

    function formatMinutes(m) {
        if (m < 60) return `${m} min`;
        const h = Math.floor(m / 60), r = m % 60;
        return r ? `${h} h ${r} min` : `${h} h`;
    }

    // ---------- Address picker ----------
    function initPicker(root) {
        const latInput = root.querySelector('[data-lat]');
        const lngInput = root.querySelector('[data-lng]');
        const search = root.querySelector('[data-search]');
        const results = root.querySelector('[data-results]');
        const picked = root.querySelector('[data-picked]');
        const etaBox = root.querySelector('[data-eta]');
        const byId = id => id ? document.getElementById(id) : null;
        const fields = {
            address: byId(root.dataset.addressId), city: byId(root.dataset.cityId),
            province: byId(root.dataset.provinceId), postal: byId(root.dataset.postalId)
        };

        const hasPoint = latInput.value && lngInput.value;
        const start = hasPoint ? [parseFloat(latInput.value), parseFloat(lngInput.value)] : [STORE.lat, STORE.lng];
        const map = baseMap(root.querySelector('[data-map]'), start[0], start[1], hasPoint ? 16 : 12);

        if (root.dataset.showEta === 'true') {
            L.marker([STORE.lat, STORE.lng], { icon: icons.store() }).addTo(map)
                .bindPopup(`<strong>${STORE.name}</strong><br>${STORE.address}`);
        }

        let marker = null, line = null;
        function setPoint(lat, lng, fillAddress) {
            latInput.value = lat.toFixed(6);
            lngInput.value = lng.toFixed(6);
            if (!marker) {
                marker = L.marker([lat, lng], { draggable: true, icon: icons.home() }).addTo(map);
                marker.on('dragend', e => { const p = e.target.getLatLng(); setPoint(p.lat, p.lng, true); });
            } else {
                marker.setLatLng([lat, lng]);
            }
            picked.textContent = `📍 ${lat.toFixed(5)}, ${lng.toFixed(5)}`;
            if (etaBox) {
                const est = estimate(STORE.lat, STORE.lng, lat, lng);
                etaBox.classList.remove('d-none');
                const fee = est.km <= STORE.baseKm ? STORE.baseFee
                    : STORE.baseFee + Math.ceil(Math.round((est.km - STORE.baseKm) * 10) / 10) * STORE.perKmFee;
                etaBox.innerHTML = `<i class="bi bi-truck me-2"></i>About <strong>${est.km} km</strong> from our store · roughly
                    <strong>${formatMinutes(est.minutes)}</strong> drive · delivery fee <strong>R ${fee.toFixed(2)}</strong>`;
                if (line) map.removeLayer(line);
                line = L.polyline([[STORE.lat, STORE.lng], [lat, lng]], { color: '#183630', weight: 3, dashArray: '6 8' }).addTo(map);
            }
            if (fillAddress) reverseGeocode(lat, lng);
            latInput.dispatchEvent(new Event('change', { bubbles: true }));
        }

        function fillFrom(a) {
            if (!a) return;
            const street = [a.house_number, a.road].filter(Boolean).join(' ');
            const suburb = a.suburb || a.neighbourhood || a.quarter || '';
            const city = a.city || a.town || a.village || a.municipality || '';
            if (fields.address) {
                const parts = [street, suburb];
                if (!fields.city) parts.push(city);
                if (!fields.postal && a.postcode) parts.push(a.postcode);
                fields.address.value = parts.filter(Boolean).join(', ');
            }
            if (fields.city && city) fields.city.value = city;
            if (fields.postal && a.postcode) fields.postal.value = a.postcode;
            if (fields.province && a.state) {
                const opt = [...fields.province.options].find(o => o.value.toLowerCase() === a.state.toLowerCase());
                if (opt) fields.province.value = opt.value;
            }
        }

        async function reverseGeocode(lat, lng) {
            try {
                const r = await fetch(`${NOMINATIM}/reverse?format=json&addressdetails=1&lat=${lat}&lon=${lng}`, { headers: { 'Accept-Language': 'en' } });
                if (r.ok) fillFrom((await r.json()).address);
            } catch { /* offline: user can type the address */ }
        }

        let timer;
        search.addEventListener('input', () => {
            clearTimeout(timer);
            const q = search.value.trim();
            if (q.length < 3) { results.classList.add('d-none'); return; }
            timer = setTimeout(async () => {
                try {
                    const r = await fetch(`${NOMINATIM}/search?format=json&addressdetails=1&countrycodes=za&limit=6&q=${encodeURIComponent(q)}`,
                        { headers: { 'Accept-Language': 'en' } });
                    const items = r.ok ? await r.json() : [];
                    results.innerHTML = items.length
                        ? items.map((it, i) => `<button type="button" data-i="${i}"><i class="bi bi-geo-alt me-2 text-muted"></i>${it.display_name}</button>`).join('')
                        : '<div class="p-2 small text-muted">No matches. Try a nearby street or suburb.</div>';
                    results.classList.remove('d-none');
                    results.querySelectorAll('button').forEach(b => b.addEventListener('click', () => {
                        const it = items[b.dataset.i];
                        const lat = parseFloat(it.lat), lng = parseFloat(it.lon);
                        map.setView([lat, lng], 17);
                        setPoint(lat, lng, false);
                        fillFrom(it.address);
                        results.classList.add('d-none');
                        search.value = it.display_name;
                    }));
                } catch { /* ignore */ }
            }, 400);
        });
        search.addEventListener('keydown', e => { if (e.key === 'Enter') e.preventDefault(); });
        document.addEventListener('click', e => { if (!root.contains(e.target)) results.classList.add('d-none'); });

        map.on('click', e => setPoint(e.latlng.lat, e.latlng.lng, true));

        root.querySelector('[data-locate]').addEventListener('click', () => {
            if (!navigator.geolocation) return alert('Your browser cannot share its location.');
            navigator.geolocation.getCurrentPosition(
                p => { map.setView([p.coords.latitude, p.coords.longitude], 17); setPoint(p.coords.latitude, p.coords.longitude, true); },
                () => alert('Could not get your location. Please allow location access, or search instead.'),
                { enableHighAccuracy: true, timeout: 10000 });
        });

        if (hasPoint) setPoint(start[0], start[1], false);
        root._map = map;
        return map;
    }

    // ---------- Read-only route map (driver & admin views) ----------
    function initRouteMap(el, dest, driver) {
        const map = baseMap(el, STORE.lat, STORE.lng, 13);
        const store = L.marker([STORE.lat, STORE.lng], { icon: icons.store() }).addTo(map).bindPopup(`<strong>${STORE.name}</strong><br>${STORE.address}`);
        const pts = [[STORE.lat, STORE.lng]];
        let home = null, drv = null;
        if (dest) {
            home = L.marker([dest.lat, dest.lng], { icon: icons.home() }).addTo(map).bindPopup(dest.label || 'Delivery address');
            pts.push([dest.lat, dest.lng]);
            L.polyline([[STORE.lat, STORE.lng], [dest.lat, dest.lng]], { color: '#183630', weight: 3, dashArray: '6 8' }).addTo(map);
        }
        function setDriver(lat, lng) {
            if (lat == null || lng == null) return;
            if (!drv) drv = L.marker([lat, lng], { icon: icons.driver(), zIndexOffset: 1000 }).addTo(map).bindPopup('Driver');
            else drv.setLatLng([lat, lng]);
        }
        if (driver) { setDriver(driver.lat, driver.lng); pts.push([driver.lat, driver.lng]); }
        if (pts.length > 1) map.fitBounds(pts, { padding: [40, 40] });
        return { map, setDriver, fit: () => { const p = [[STORE.lat, STORE.lng]]; if (dest) p.push([dest.lat, dest.lng]); if (drv) p.push(drv.getLatLng()); map.fitBounds(p, { padding: [40, 40] }); } };
    }

    window.StoreMaps = { initPicker, initRouteMap, estimate, formatMinutes };
    document.addEventListener('DOMContentLoaded', () => document.querySelectorAll('[data-map-picker]').forEach(initPicker));
})();
