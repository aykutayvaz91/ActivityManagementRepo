// ActivityManagement - Global JS

// ABP bildirimlerini Türkçe yapılandır
if (typeof abp !== 'undefined' && abp.notify) {
    // ABP notify zaten tanımlı
}

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
        form.addEventListener('submit', function () {
            var btn = form.querySelector('button[type=submit]');
            if (btn) {
                btn.disabled = true;
                btn.innerHTML = '<span class="spinner-border spinner-border-sm me-1"></span>İşleniyor...';
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
