$(document).ready(function () {
            const $drawer = $("#quoteDrawer");
            const $overlay = $("#quoteDrawerOverlay");
            const $body = $("body");

            function openDrawer() {
                $drawer.addClass("is-open").attr("aria-hidden", "false");
                $overlay.addClass("is-open");
                $body.addClass("quote-drawer-open");
                $("#SlideFormStartedAt").val(Date.now());
            }

            function closeDrawer() {
                $drawer.removeClass("is-open").attr("aria-hidden", "true");
                $overlay.removeClass("is-open");
                $body.removeClass("quote-drawer-open");
            }

            $("#quoteDrawerToggle").on("click", function () {
                openDrawer();
            });

            $("#quoteDrawerClose, #quoteDrawerOverlay").on("click", function () {
                closeDrawer();
            });

            $(document).on("keydown", function (e) {
                if (e.key === "Escape") {
                    closeDrawer();
                }
            });
            $("#SlideFormStartedAt").val(Date.now());
            $("#slideQuoteForm").on("submit", function (e) {
                e.preventDefault();

                const $form = $(this);
                const $submitBtn = $form.find("button[type='submit']");
                const token = $form.find("input[name='__RequestVerificationToken']").val();

                const entity = {
                    __RequestVerificationToken: token,
                    Name: $("#SlideName").val(),
                    Phone: $("#SlidePhone").val(),
                    Email: $("#SlideEmail").val(),
                    Sector: $("#SlideSector").val(),
                    Account: $("#SlideAccount").val(),
                    Subject: $("#SlideSubject").val(),
                    Message: $("#SlideMessage").val(),
                    Website: $("#SlideWebsite").val(),
                    FormStartedAt: $("#SlideFormStartedAt").val(),
                    SecurityAnswer: $("#SlideSecurityAnswer").val(),
                    SecurityQuestionToken: $("#SlideSecurityQuestionToken").val()
                };

                $("#slideQuoteSuccess").hide();
                $("#slideQuoteError").hide().html("");

                $submitBtn.prop("disabled", true).text("Gönderiliyor...");

                $.ajax({
                    method: "POST",
                    url: "/Contact/Index",
                    dataType: "json",
                    data: entity,
                    success: function (res) {
                        if (res.success) {
                            $("#slideQuoteForm")[0].reset();
                            $("#SlideFormStartedAt").val(Date.now());
                            $("#slideQuoteSuccess").show();
                            $("#slideQuoteError").hide();

                            setTimeout(function () {
                                closeDrawer();
                                $("#slideQuoteSuccess").hide();
                            }, 1200);
                        } else {
                            $("#slideQuoteError").show().html(res.message || "Bir hata oluştu.");
                            $("#slideQuoteSuccess").hide();
                        }
                    },
                    error: function (xhr) {
                        let errorMessage = "Sunucu hatası!";

                        if (xhr.responseJSON && xhr.responseJSON.message) {
                            errorMessage = xhr.responseJSON.message;
                        }

                        $("#slideQuoteError").show().html(errorMessage);
                        $("#slideQuoteSuccess").hide();
                    },
                    complete: function () {
                        $submitBtn.prop("disabled", false).text("Gönder");
                    }
                });
            });
        });
