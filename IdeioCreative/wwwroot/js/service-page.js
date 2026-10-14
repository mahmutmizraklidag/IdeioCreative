// Javascript tarafında da sayfa hazır olur olmaz preloader'ı yok edelim
    document.addEventListener("DOMContentLoaded", function() {
        var preloader = document.getElementById('preloader') || document.querySelector('.preloader');
        if (preloader) {
            preloader.style.display = 'none';
        }
        // Body üzerindeki olası scroll kilit class'larını temizle
        document.body.style.overflow = 'auto';
        document.body.classList.remove('hidden', 'overflow-hidden');
    });
