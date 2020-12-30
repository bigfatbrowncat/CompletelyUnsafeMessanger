(function () {
	// Checking dependencies
	if (showdown === undefined) {
		console.error("Showdown not found in the global NS. Include showdown.js before nanowiki.js (showdown v 1.9.1 - 02-11-2019 is supported)");
	}

	wikiTitles = {}
	wikiUrls = {}

	showdown.extension('setup-wiki-links', function () {
	  return [{
		type: 'listener',
		listeners: {
			'hashCodeTags.after': function (event, text, showdownObject, options, globals) {
				// Quotes
				var quoted_regex = /('')([^(?:'')]*)('')/g;
				var res = text.replaceAll(quoted_regex, Document.prototype.nanowiki.config.quotes.left + "$2" + Document.prototype.nanowiki.config.quotes.right)

				// Em and En-dashes
				var em_regex = /---(\s)/g;
				var res = res.replaceAll(em_regex, "&#8212;$1")
				var en_regex = /--(\s)/g;
				var res = res.replaceAll(en_regex, "&#8211;$1")
			
				return res;
			},
	  
		  'anchors.before': function (event, text, showdownObject, options, globals) {
			Object.keys(wikiUrls).forEach(function(key) {
				globals.gUrls[key] = wikiUrls[key];
			});
			Object.keys(wikiTitles).forEach(function(key) {
				globals.gTitles[key] = wikiTitles[key];
			});
		
			return text;
		  }
		}
	  }]
	});
	var showdownConverter = new showdown.Converter({ extensions: [ 'setup-wiki-links' ] });

	function update_links(id_list) {
		wikiTitles = []
		wikiUrls = []

		for (var i = 0; i < id_list.length; i++) {
			wikiUrls[id_list[i]] = "#" + id_list[i];
		}
	}

	function convert(text) {
		return showdownConverter.makeHtml(text);
	}

	var nanowiki = {
		update_links: update_links,
		convert: convert,

		config: {
			quotes: {
				left: '&ldquo;',
				right: '&rdquo;'
			}
		}
	};

	Document.prototype.nanowiki = nanowiki;
})();

// Global scope object
this.nanowiki = Document.prototype.nanowiki;
