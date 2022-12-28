$(document).ready(function () {
    function getFileName(fullPath) {
        return fullPath.replace(/^.*[\\\/]/, '');
    }

    var $download = $('#downloadButton');
    var $icon = $('#downloadButton i');
    var $form = $('#imageFileInputForm');
    var $select = $('#selectPlatforms');
    var $fileName = $('#fileName');
    var $fileLabel = $('#fileNameLabel');
    var $inputColor = $('.input-color');
    var $colorChanged = $('#colorChanged');
    var $color = $('#color');
    var $colorOption = $('.colorOption');
    var $colorPickers = $('.colorpickers');

    function showIndicator(show) {
        if (show) {
            $icon.addClass('fa-circle-o-notch fa-spin');
        } else {
            $icon.removeClass('fa-circle-o-notch fa-spin');
        }
    }

    function postToApi() {
        try {
            showIndicator(true);
            var formdata = new FormData($form[0]);
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

    $download.on('click', function (evt) {
        evt.preventDefault();
        postToApi();
    });

    $select.on('click', function (evt) {
        evt.preventDefault();
        var allChecked = true;
        var platforms = $('input[name="platform"]');
        platforms.each(function () {
            allChecked = allChecked & this.checked;
        });

        platforms.prop('checked', !allChecked);
    });

    $fileName.change(function (e) {
        //if new value is valid
        if (e.currentTarget.value) {
            $fileLabel.text(getFileName(e.currentTarget.value));
            $download.prop('disabled', false);
            $download.addClass('isEnabled');
        } else {
            $fileLabel.text('Choose File');
            $download.prop('disabled', true);
            $download.removeClass('isEnabled');
        }
    });

    $inputColor.change(function () {
        $colorChanged.val(1);
        $color.val($(this).val());
    });

    $color.change(function () {
        $colorChanged.val(1);
        $inputColor.val($(this).val());
    });

    var colorOptions = {
        transparent: 0,
        color: 1
    };

    var colorValues = {
        isEmpty: 0,
        isChanged: 1
    };

    $colorOption.on('change', function () {
        var colorOption = parseInt($(this).val(), 10);

        if (colorOption === colorOptions.color) {
            $colorChanged.val(colorValues.isChanged);
            $colorPickers.fadeIn();
            return;
        }

        $colorChanged.val(colorValues.isEmpty);
        $colorPickers.fadeOut();
    });

});
