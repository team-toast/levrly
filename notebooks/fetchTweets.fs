// fetch the last 10 tweets for @schalkdormehl using F#


let fetchTweets = function (name) {
    return fetch("https://twitter.com/" + name + "/with_replies")
        .then(function (response) {
            return response.text();
        })
        .then(function (html) {
            const parser = new DOMParser();
            const doc = parser.parseFromString(html, "text/html");
            const tweets = doc.querySelectorAll(".tweet-text");
            return [...tweets].map(function (tweet) {
                return tweet.textContent;
            });
        });
};