(() => {
    "use strict";

    const frames = Array.from(document.querySelectorAll(".youtube-player"));
    if (!frames.length) return;

    frames.forEach(frame => {
        const source = new URL(frame.dataset.src);
        source.searchParams.set("origin", window.location.origin);
        source.searchParams.set("cc_load_policy", "0");
        frame.src = source.href;
    });

    function disableCaptions(event) {
        const target = event.target;
        const modules = typeof target.getOptions === "function" ? target.getOptions() : [];
        if (!Array.isArray(modules) || !modules.includes("captions")) return;

        // These optional player extensions also clear captions enabled by viewer preferences.
        if (typeof target.setOption === "function") target.setOption("captions", "track", {});
        if (typeof target.unloadModule === "function") target.unloadModule("captions");
    }

    function connectPlayer(frame) {
        const cover = frame.closest("[data-youtube-cover]");
        const soundToggle = frame.dataset.soundToggle && document.getElementById(frame.dataset.soundToggle);
        let revealTimer;

        function showCover() {
            window.clearTimeout(revealTimer);
            if (cover) cover.classList.remove("is-playing");
        }

        function updateSoundToggle(target) {
            if (!soundToggle) return;
            const silent = target.isMuted() || target.getVolume() === 0;
            const label = silent ? "Videonun sesini aç" : "Videonun sesini kapat";
            soundToggle.setAttribute("aria-pressed", String(!silent));
            soundToggle.setAttribute("aria-label", label);
            soundToggle.title = label;
            soundToggle.querySelector("span").textContent = silent ? "Sesi aç" : "Sesi kapat";
            soundToggle.querySelector("i").classList.toggle("fa-volume-mute", silent);
            soundToggle.querySelector("i").classList.toggle("fa-volume-up", !silent);
        }

        new window.YT.Player(frame, {
            events: {
                onReady: function(event) {
                    disableCaptions(event);
                    event.target.mute();
                    event.target.playVideo();
                    if (soundToggle) {
                        soundToggle.addEventListener("click", function() {
                            if (event.target.isMuted() || event.target.getVolume() === 0) {
                                event.target.setVolume(100);
                                event.target.unMute();
                            } else {
                                event.target.mute();
                            }
                            event.target.playVideo();
                            updateSoundToggle(event.target);
                        });
                        updateSoundToggle(event.target);
                        soundToggle.hidden = false;
                    }
                },
                onApiChange: disableCaptions,
                onStateChange: function(event) {
                    disableCaptions(event);
                    updateSoundToggle(event.target);
                    showCover();
                    if (cover && event.data === window.YT.PlayerState.PLAYING) {
                        // Let the initial player animation settle before revealing the video.
                        revealTimer = window.setTimeout(function() {
                            if (event.target.getPlayerState() === window.YT.PlayerState.PLAYING) {
                                cover.classList.add("is-playing");
                            }
                        }, 1000);
                    }
                },
                onError: showCover,
                onAutoplayBlocked: showCover
            }
        });
    }

    let connected = false;
    function connectPlayers() {
        if (connected) return;
        connected = true;
        frames.forEach(connectPlayer);
    }

    if (window.YT && window.YT.Player) {
        connectPlayers();
        return;
    }

    const previousReady = window.onYouTubeIframeAPIReady;
    window.onYouTubeIframeAPIReady = function() {
        if (typeof previousReady === "function") previousReady();
        connectPlayers();
    };

    if (!document.querySelector('script[src="https://www.youtube.com/iframe_api"]')) {
        const script = document.createElement("script");
        script.src = "https://www.youtube.com/iframe_api";
        script.async = true;
        document.head.appendChild(script);
    }
})();
