(() => {
    "use strict";

    const form = document.getElementById("audit-form");
    if (!form) return;

    const fields = document.getElementById("audit-form-fields");
    const error = document.getElementById("audit-form-error");
    const success = document.getElementById("audit-form-success");
    const submit = form.querySelector("button[type='submit']");
    const startedAt = form.elements.namedItem("FormStartedAt");
    const submitLabel = submit.innerHTML;
    let sending = false;
    startedAt.value = String(Date.now());

    // A visitor who has not started advertising should not also select an active platform.
    const platforms = Array.from(form.querySelectorAll("input[name='AuditPlatforms']"));
    platforms.forEach(input => input.addEventListener("change", () => {
        if (!input.checked) return;
        const notStarted = input.value === "Henüz reklam vermiyorum";
        platforms.forEach(other => {
            if (other !== input && (notStarted || other.value === "Henüz reklam vermiyorum")) {
                other.checked = false;
            }
        });
    }));

    function showError(message) {
        error.textContent = message;
        error.hidden = false;
        error.focus();
    }

    form.addEventListener("submit", async event => {
        event.preventDefault();
        if (sending || !form.reportValidity()) return;
        error.hidden = true;

        const payload = new FormData(form);
        const selectedPlatforms = payload.getAll("AuditPlatforms").join(", ") || "Belirtilmedi";
        const notes = String(payload.get("AuditNotes") || "").trim();
        payload.set("Message", [
            "Ücretsiz reklam denetimi talep ediyorum.",
            `Reklam platformları: ${selectedPlatforms}`,
            `Aylık reklam bütçesi: ${payload.get("AuditBudget") || "Belirtilmedi"}`,
            notes ? `Başvuru notu: ${notes}` : ""
        ].filter(Boolean).join("\n"));
        payload.delete("AuditPlatforms");
        payload.delete("AuditBudget");
        payload.delete("AuditNotes");

        sending = true;
        submit.disabled = true;
        submit.textContent = "Başvurunuz gönderiliyor…";
        form.setAttribute("aria-busy", "true");
        const controller = new AbortController();
        const timeout = window.setTimeout(() => controller.abort(), 60000);

        try {
            const response = await fetch(form.action, {
                method: "POST",
                body: payload,
                credentials: "same-origin",
                headers: { "Accept": "application/json", "X-Requested-With": "XMLHttpRequest" },
                signal: controller.signal
            });
            if (response.status === 429) {
                showError("Çok fazla başvuru denemesi yaptınız. Lütfen bir dakika sonra tekrar deneyin.");
                return;
            }
            if (response.status === 400) {
                showError("Güvenlik doğrulaması tamamlanamadı. Sayfayı yenileyip tekrar deneyin.");
                return;
            }
            if (!response.ok) throw new Error("server");
            const result = await response.json();
            if (result.success !== true) {
                showError(result.message || "Başvurunuz kaydedilemedi. Lütfen tekrar deneyin.");
                return;
            }

            fields.hidden = true;
            fields.disabled = true;
            success.hidden = false;
            success.focus();
            document.querySelector(".audit-form-intro").hidden = true;
            // Only signal a lead after the contact endpoint confirms it was saved.
            try {
                if (typeof window.oaiq === "function" && result.conversionEventId) {
                    window.oaiq("measure", "lead_created", { type: "customer_action" },
                        { event_id: result.conversionEventId });
                }
            } catch {
                // A blocked pixel must not change a confirmed application result or other tracking.
            }
            try {
                window.dataLayer = window.dataLayer || [];
                window.dataLayer.push({ event: "generate_lead", lead_source: "free_ad_audit" });
                if (typeof window.gtag === "function") {
                    window.gtag("event", "conversion", { send_to: "AW-18008259244/qVNKCInLy5UdEKz1gItD" });
                }
                if (typeof window.fbq === "function") window.fbq("track", "Lead");
            } catch {
                // A blocked analytics script must not change a confirmed application result.
            }
        } catch (exception) {
            showError(exception.name === "AbortError"
                ? "Sunucudan yanıt alınamadı. Başvurunuz kaydedilmiş olabilir; tekrar göndermeden önce bizimle iletişime geçebilirsiniz."
                : "Başvurunuzun sonucu doğrulanamadı. Lütfen bağlantınızı kontrol edin veya bizimle iletişime geçin.");
        } finally {
            window.clearTimeout(timeout);
            sending = false;
            submit.disabled = false;
            submit.innerHTML = submitLabel;
            form.removeAttribute("aria-busy");
        }
    });

    // Keep the mobile call to action from covering the application form or footer.
    const mobileCta = document.querySelector(".audit-mobile-cta");
    if (mobileCta && "IntersectionObserver" in window) {
        const visible = new Set();
        const observer = new IntersectionObserver(entries => {
            entries.forEach(entry => entry.isIntersecting ? visible.add(entry.target) : visible.delete(entry.target));
            mobileCta.classList.toggle("is-hidden", visible.size > 0);
        });
        observer.observe(document.getElementById("basvuru"));
        observer.observe(document.querySelector(".audit-footer"));
    }
})();
