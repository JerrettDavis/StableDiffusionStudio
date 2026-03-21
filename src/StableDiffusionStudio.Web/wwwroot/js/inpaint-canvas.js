window.inpaintCanvas = {
    init: function (canvasId, imageUrl) {
        const canvas = document.getElementById(canvasId);
        if (!canvas) return;
        const ctx = canvas.getContext('2d');
        const img = new Image();
        img.onload = function () {
            canvas.width = img.width;
            canvas.height = img.height;

            // Create mask canvas (black = keep, white = inpaint)
            canvas._maskCanvas = document.createElement('canvas');
            canvas._maskCanvas.width = img.width;
            canvas._maskCanvas.height = img.height;
            canvas._maskCtx = canvas._maskCanvas.getContext('2d');
            // Fill mask with black (keep everything by default)
            canvas._maskCtx.fillStyle = 'black';
            canvas._maskCtx.fillRect(0, 0, img.width, img.height);

            // Store original image for redrawing
            canvas._bgImage = img;
            canvas._drawing = false;
            canvas._brushSize = 20;

            // Draw the background image
            ctx.drawImage(img, 0, 0);
        };
        img.src = imageUrl;

        // Remove old listeners by replacing the canvas node (clean approach)
        const newCanvas = canvas.cloneNode(true);
        canvas.parentNode.replaceChild(newCanvas, canvas);
        const freshCanvas = document.getElementById(canvasId);
        const freshCtx = freshCanvas.getContext('2d');

        // Re-run init on the fresh canvas
        const freshImg = new Image();
        freshImg.onload = function () {
            freshCanvas.width = freshImg.width;
            freshCanvas.height = freshImg.height;

            freshCanvas._maskCanvas = document.createElement('canvas');
            freshCanvas._maskCanvas.width = freshImg.width;
            freshCanvas._maskCanvas.height = freshImg.height;
            freshCanvas._maskCtx = freshCanvas._maskCanvas.getContext('2d');
            freshCanvas._maskCtx.fillStyle = 'black';
            freshCanvas._maskCtx.fillRect(0, 0, freshImg.width, freshImg.height);

            freshCanvas._bgImage = freshImg;
            freshCanvas._drawing = false;
            freshCanvas._brushSize = 20;

            freshCtx.drawImage(freshImg, 0, 0);
        };
        freshImg.src = imageUrl;

        function getPos(e) {
            const rect = freshCanvas.getBoundingClientRect();
            return {
                x: (e.clientX - rect.left) * (freshCanvas.width / rect.width),
                y: (e.clientY - rect.top) * (freshCanvas.height / rect.height)
            };
        }

        function paint(e) {
            if (!freshCanvas._drawing) return;
            const pos = getPos(e);

            // Draw on visible canvas (semi-transparent red overlay)
            freshCtx.fillStyle = 'rgba(255, 0, 0, 0.4)';
            freshCtx.beginPath();
            freshCtx.arc(pos.x, pos.y, freshCanvas._brushSize, 0, Math.PI * 2);
            freshCtx.fill();

            // Draw on mask canvas (white = inpaint area)
            freshCanvas._maskCtx.fillStyle = 'white';
            freshCanvas._maskCtx.beginPath();
            freshCanvas._maskCtx.arc(pos.x, pos.y, freshCanvas._brushSize, 0, Math.PI * 2);
            freshCanvas._maskCtx.fill();
        }

        freshCanvas.addEventListener('mousedown', function (e) {
            freshCanvas._drawing = true;
            paint(e);
        });
        freshCanvas.addEventListener('mouseup', function () { freshCanvas._drawing = false; });
        freshCanvas.addEventListener('mouseleave', function () { freshCanvas._drawing = false; });
        freshCanvas.addEventListener('mousemove', paint);

        // Touch support for tablets
        freshCanvas.addEventListener('touchstart', function (e) {
            e.preventDefault();
            freshCanvas._drawing = true;
            var touch = e.touches[0];
            paint({ clientX: touch.clientX, clientY: touch.clientY });
        });
        freshCanvas.addEventListener('touchend', function () { freshCanvas._drawing = false; });
        freshCanvas.addEventListener('touchmove', function (e) {
            e.preventDefault();
            var touch = e.touches[0];
            paint({ clientX: touch.clientX, clientY: touch.clientY });
        });
    },

    setBrushSize: function (canvasId, size) {
        const canvas = document.getElementById(canvasId);
        if (canvas) canvas._brushSize = size;
    },

    getMaskData: function (canvasId) {
        const canvas = document.getElementById(canvasId);
        if (!canvas || !canvas._maskCanvas) return null;
        return canvas._maskCanvas.toDataURL('image/png');
    },

    clearMask: function (canvasId, imageUrl) {
        const canvas = document.getElementById(canvasId);
        if (!canvas) return;
        const ctx = canvas.getContext('2d');

        // Reset mask to all black
        if (canvas._maskCtx) {
            canvas._maskCtx.fillStyle = 'black';
            canvas._maskCtx.fillRect(0, 0, canvas._maskCanvas.width, canvas._maskCanvas.height);
        }

        // Redraw original image
        if (canvas._bgImage) {
            ctx.clearRect(0, 0, canvas.width, canvas.height);
            ctx.drawImage(canvas._bgImage, 0, 0);
        }
    }
};
