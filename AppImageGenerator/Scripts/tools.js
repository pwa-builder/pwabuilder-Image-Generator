﻿$(document).ready(function () {
    function getFileName(fullPath) {
        return fullPath.replace(/^.*[\\\/]/, '');
    }

    function postToApi() {
        try {
            var formdata = new FormData($('#imageFileInputForm')[0]);
            $.ajax({
                url: '/api/image',
                type: 'POST',
                data: formdata,
                accepts: "application/json",
                success: function (req) {
                    $("body").append("<iframe src='" + req.Uri + "' style='display: none;' ></iframe>");
                },
                error: function (req, err) {
                    var resp = JSON.parse(req.responseText);
                    alert(resp.Message);
                },
                enctype: 'multipart/form-data',
                cache: false,
                contentType: false,
                processData: false
            });

        } catch (e) {
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
    
    $('#fileInput').change(function(e) {
        //if new value is valid
        if (e.currentTarget.value) {
            $('#fileName').val(getFileName(e.currentTarget.value))
            $('#downloadButton').prop('disabled', false);
            $('#downloadButton').addClass('isEnabled');
        } else {
            $('#downloadButton').prop('disabled', true);
            $('#downloadButton').removeClass('isEnabled');
        }
    });
});    