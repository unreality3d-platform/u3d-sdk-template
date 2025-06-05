mergeInto(LibraryManager.library, {

    UnityCallTestFunction: function () {
        if (typeof window.UnityCallTestFunction === 'function') {
            window.UnityCallTestFunction();
        }
    },

    UnityCheckContentAccess: function (contentIdPtr) {
        var contentId = UTF8ToString(contentIdPtr);
        if (typeof window.UnityCheckContentAccess === 'function') {
            window.UnityCheckContentAccess(contentId);
        }
    },

    UnityRequestPayment: function (contentIdPtr, pricePtr) {
        var contentId = UTF8ToString(contentIdPtr);
        var price = UTF8ToString(pricePtr);
        if (typeof window.UnityRequestPayment === 'function') {
            window.UnityRequestPayment(contentId, price);
        }
    }

});