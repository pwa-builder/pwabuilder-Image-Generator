$(document).ready(function () {
    function getFileName(fullPath) {
        return fullPath.replace(/^.*[\\\/]/, '');
    }

    function showIndicator(show) {
        if (show) {
            $('#downloadButton i').removeClass('fa-download').addClass('fa-circle-o-notch fa-spin');
        } else {
            $('#downloadButton i').removeClass('fa-circle-o-notch fa-spin').addClass('fa-download');
        }
    }

    function postToApi() {
        try {
            showIndicator(true);
            var formdata = new FormData($('#imageFileInputForm')[0]);
            $.ajax({
                url: '/api/image',
                type: 'POST',
                data: formdata,
                accepts: {
                    json: "application/json"
                },
                dataType: "json",
                success: function (req) {
                    showIndicator(false);
                    $("body").append("<iframe src='" + req.Uri + "' style='display: none;' ></iframe>");
                },
                error: function (req, err) {
                    showIndicator(false);
                    var resp = JSON.parse(req.responseText);
                    alert(resp.Message);
                },
                enctype: 'multipart/form-data',
                cache: false,
                contentType: false,
                processData: false
            });

        } catch (e) {
            showIndicator(false);
            alert(e);
        }
    };

    $('#downloadButton').on("click", function (evt) {
        evt.preventDefault();
        postToApi();
    });

    $('#selectPlatforms').on("click", function (evt) {
        evt.preventDefault();
        var allChecked = true;
        var platforms = $('input[name="platform"]');
        platforms.each(function () {
            allChecked = allChecked & this.checked;
        });

        platforms.prop('checked', !allChecked);
    });

    $('#fileName').change(function (e) {
        //if new value is valid
        if (e.currentTarget.value) {
            $('#fileNameLabel').text(getFileName(e.currentTarget.value));
            $('#downloadButton').prop('disabled', false);
            $('#downloadButton').addClass('isEnabled');
        } else {
            $('#fileNameLabel').text('Choose File');
            $('#downloadButton').prop('disabled', true);
            $('#downloadButton').removeClass('isEnabled');
        }
    });

    $('input[type="color"]').change(function () {
        $('#colorChanged').val(1);
        $('#color').val($(this).val());
    });

    $('#color').change(function () {
        $('#colorChanged').val(1);
        $('input[type="color"]').val($(this).val());
    });

});
