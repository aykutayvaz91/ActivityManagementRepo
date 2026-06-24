// ActivityManagement - Global JS

// abp.js yüklü değil — hafif bir shim (toast bildirimleri + confirm)
(function () {
    if (typeof window.abp === 'undefined') window.abp = {};
    if (!window.abp.notify) {
        function toast(msg, type) {
            var colors = { success: 'success', error: 'danger', warn: 'warning', info: 'info' };
            var c = colors[type] || 'secondary';
            var wrap = document.getElementById('amToastWrap');
            if (!wrap) {
                wrap = document.createElement('div');
                wrap.id = 'amToastWrap';
                wrap.style.cssText = 'position:fixed;top:1rem;right:1rem;z-index:1080;display:flex;flex-direction:column;gap:.5rem';
                document.body.appendChild(wrap);
            }
            var el = document.createElement('div');
            el.className = 'alert alert-' + c + ' shadow-sm mb-0 py-2 px-3';
            el.style.cssText = 'min-width:240px;opacity:0;transition:opacity .2s';
            el.innerHTML = msg;
            wrap.appendChild(el);
            requestAnimationFrame(function () { el.style.opacity = '1'; });
            setTimeout(function () { el.style.opacity = '0'; setTimeout(function () { el.remove(); }, 250); }, 3500);
        }
        window.abp.notify = {
            success: function (m) { toast(m, 'success'); },
            error: function (m) { toast(m, 'error'); },
            warn: function (m) { toast(m, 'warn'); },
            info: function (m) { toast(m, 'info'); }
        };
    }
    if (!window.abp.message) {
        window.abp.message = {
            confirm: function (m, cb) { if (typeof cb === 'function') cb(window.confirm(m)); }
        };
    }
})();

// Tarih formatı - Türkçe
function formatDate(dateStr) {
    if (!dateStr) return '-';
    return new Date(dateStr).toLocaleDateString('tr-TR');
}

function formatDateTime(dateStr) {
    if (!dateStr) return '-';
    return new Date(dateStr).toLocaleString('tr-TR');
}

// Silme onayı yardımcısı
function confirmDelete(message, callback) {
    if (confirm(message || 'Silmek istediğinizden emin misiniz?')) {
        callback();
    }
}

// Form submit yüklenme durumu
document.addEventListener('DOMContentLoaded', function () {
    // Bootstrap tooltip başlat
    var tooltipTriggerList = [].slice.call(document.querySelectorAll('[data-bs-toggle="tooltip"]'));
    tooltipTriggerList.map(function (el) {
        return new bootstrap.Tooltip(el);
    });

    // Form submit - loading göstergesi
    document.querySelectorAll('form').forEach(function (form) {
        form.addEventListener('submit', function (e) {
            // HTML5 validasyon geçmediyse butona dokunma
            if (!form.checkValidity()) return;
            var btn = form.querySelector('button[type=submit]');
            if (btn) {
                var orig = btn.innerHTML;
                btn.disabled = true;
                btn.innerHTML = '<span class="spinner-border spinner-border-sm me-1"></span>İşleniyor...';
                setTimeout(function () { btn.disabled = false; btn.innerHTML = orig; }, 10000);
            }
        });
    });

    // Otomatik alert kapat
    setTimeout(function () {
        document.querySelectorAll('.alert.alert-success, .alert.alert-info').forEach(function (el) {
            var alert = bootstrap.Alert.getOrCreateInstance(el);
            if (alert) alert.close();
        });
    }, 4000);
});

// AJAX hata yönetimi (ABP uyumlu)
$(document).ajaxError(function (event, xhr) {
    if (xhr.status === 401) {
        window.location.href = '/Account/Login';
    } else if (xhr.status === 403) {
        abp.notify.error('Bu işlem için yetkiniz bulunmamaktadır.');
    }
});

// Table filtre kolaylık fonksiyonu
function filterTable(tableId, inputId) {
    var input = document.getElementById(inputId);
    if (!input) return;
    input.addEventListener('keyup', function () {
        var filter = this.value.toLowerCase();
        var rows = document.querySelectorAll('#' + tableId + ' tbody tr');
        rows.forEach(function (row) {
            var text = row.textContent.toLowerCase();
            row.style.display = text.includes(filter) ? '' : 'none';
        });
    });
}
