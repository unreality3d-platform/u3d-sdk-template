mergeInto(LibraryManager.library, {

    // Existing PayPal functions (UNCHANGED)
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
    },

    // NEW Photon Fusion functions
    UnityGetPhotonToken: function (roomNamePtr, contentIdPtr) {
        var roomName = UTF8ToString(roomNamePtr);
        var contentId = UTF8ToString(contentIdPtr);
        if (typeof window.UnityGetPhotonToken === 'function') {
            window.UnityGetPhotonToken(roomName, contentId);
        }
    },

    UnityCreateMultiplayerSession: function (contentIdPtr, sessionNamePtr, maxPlayersPtr) {
        var contentId = UTF8ToString(contentIdPtr);
        var sessionName = UTF8ToString(sessionNamePtr);
        var maxPlayers = UTF8ToString(maxPlayersPtr);
        if (typeof window.UnityCreateMultiplayerSession === 'function') {
            window.UnityCreateMultiplayerSession(contentId, sessionName, maxPlayers);
        }
    },

    UnityJoinMultiplayerSession: function (roomNamePtr) {
        var roomName = UTF8ToString(roomNamePtr);
        if (typeof window.UnityJoinMultiplayerSession === 'function') {
            window.UnityJoinMultiplayerSession(roomName);
        }
    }

});