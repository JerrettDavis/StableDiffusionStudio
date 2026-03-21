window.downloadFile = function (filename, contentType, base64) {
    var link = document.createElement('a');
    link.href = 'data:' + contentType + ';base64,' + base64;
    link.download = filename;
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
};
