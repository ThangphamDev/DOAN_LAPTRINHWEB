document.addEventListener('DOMContentLoaded', () => {
    const wishlistButtons = document.querySelectorAll('.add-to-wishlist-btn');
    const wishlistKey = 'myWishlist';     function getWishlist() {
        const wishlist = localStorage.getItem(wishlistKey);
        return wishlist ? JSON.parse(wishlist) : [];
    }
    function saveWishlist(wishlist) {
        localStorage.setItem(wishlistKey, JSON.stringify(wishlist));
    }

    function isProductInWishlist(productId) {
        const wishlist = getWishlist();
        return wishlist.some(item => item.id === productId);
    }
    function addProductToWishlist(productData) {
        let wishlist = getWishlist();
        if (!isProductInWishlist(productData.id)) {
            wishlist.push(productData);
            saveWishlist(wishlist);
            console.log(`Đã thêm sản phẩm ${productData.name} vào danh sách yêu thích.`);
            return true; 
        }
        return false;
    }
    function removeProductFromWishlist(productId) {
        let wishlist = getWishlist();
        const initialLength = wishlist.length;
        wishlist = wishlist.filter(item => item.id !== productId);
        saveWishlist(wishlist);
        if (wishlist.length < initialLength) {
            console.log(`Đã xóa sản phẩm ID ${productId} khỏi danh sách yêu thích.`);
            return true; 
        }
        return false; 
    }
    function updateHeartIcon(button, productId) {
        const icon = button.querySelector('i');
        if (icon) { 
            if (isProductInWishlist(productId)) {
                icon.classList.remove('far');
                icon.classList.add('fas'); 
                icon.style.color = 'red'; 
            } else {
                icon.classList.remove('fas'); 
                icon.classList.add('far'); 
                icon.style.color = ''; 
            }
        }
    }

    wishlistButtons.forEach(button => {
        const productId = button.dataset.productId;
        updateHeartIcon(button, productId);
    });

    wishlistButtons.forEach(button => {
        button.addEventListener('click', (event) => {
            event.stopPropagation(); 
            const productId = button.dataset.productId;
            let productData = {};
            const productElement = button.closest('.product-item') || button.closest('.product-details-container'); 

            if (productElement) {
                const productNameElement = productElement.querySelector('h2') || productElement.querySelector('h3'); 
                const productPriceElement = productElement.querySelector('.price');
                const productImageElement = productElement.querySelector('img.main-product-image') || productElement.querySelector('.product-gallery img'); 
                productData = {
                    id: productId,
                    name: productNameElement ? productNameElement.innerText : `Sản phẩm ID: ${productId}`,
                    price: productPriceElement ? productPriceElement.innerText : 'Giá không rõ',
                    image: productImageElement ? productImageElement.src : 'placeholder.jpg'
                };
            } else {
                console.warn('Không tìm thấy phần tử cha chứa thông tin sản phẩm. Lấy dữ liệu tối thiểu.');
                productData = { id: productId, name: `Sản phẩm ID: ${productId}` }; 
            }

            if (isProductInWishlist(productId)) {
                removeProductFromWishlist(productId);
            } else {
                addProductToWishlist(productData);
            }
            updateHeartIcon(button, productId);
        });
    });
});