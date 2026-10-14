$(document).ready(function () {
        $("#ContactFormStartedAt").val(Date.now());

        $("#iletisimFormu").on("submit", function (e) {
            e.preventDefault();

            var form = $(this);
            var formData = form.serialize();

            $.ajax({
                type: "POST",
                url: "/Contact/Index",
                data: formData,
                success: function (res) {
                    if (res.success) {
                        $("#iletisimFormu")[0].reset();
                        $("#ContactFormStartedAt").val(Date.now());
                        $(".sent-message").show();
                        $(".error-message").hide().html("");
                    } else {
                        $(".error-message").show().html(res.message);
                        $(".sent-message").hide();
                    }
                },
                error: function (xhr) {
                    $(".sent-message").hide();

                    if (xhr.status === 400) {
                        $(".error-message").show().html("Güvenlik doğrulaması başarısız oldu.");
                    } else if (xhr.status === 429) {
                        $(".error-message").show().html("Çok fazla istek gönderdiniz. Lütfen biraz bekleyin.");
                    } else {
                        $(".error-message").show().html("Sunucu hatası!");
                    }
                }
            });
        });
    });
