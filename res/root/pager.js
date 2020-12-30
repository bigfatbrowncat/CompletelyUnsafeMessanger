(function () {
	// Checking dependencies
	if (nanowiki === undefined) {
		console.error("Nanowiki not found in the global NS. Include nanowiki.js before text_card_editor.js");
	}

	function find_page_by_id(pageId) {
		var pages = document.getElementsByTagName(Document.prototype.pager.config.tags.page);
		for (var i = 0; i < pages.length; i++) {
			if (pages[i].getAttribute("id") == pageId) {
				return pages[i];
			}
		}
		return null;
	}

	function show_page(pageId) {
		var pages = document.getElementsByTagName(Document.prototype.pager.config.tags.page);
		for (var i = 0; i < pages.length; i++) {
			if (pages[i].getAttribute("id") == pageId) {
				pages[i].style.display = "block";
			} else {
				pages[i].style.display = "none";
			}
		}
	}

	function find_page_template_by_id(pageTemplateId) {
		var pageTemplates = document.getElementsByTagName(Document.prototype.pager.config.tags.page_template);
		for (var i = 0; i < pageTemplates.length; i++) {
			if (pageTemplates[i].getAttribute("id") == pageTemplateId) {
				return pageTemplates[i];
			}
		}
		return null;
	}

	function create_page(pageId, pageTemplateId) {
		var pageTemplate = find_page_template_by_id(pageTemplateId);
		var newPage = document.createElement(Document.prototype.pager.config.tags.page);
		newPage.setAttribute("id", pageId);
		newPage.innerHTML = pageTemplate.innerHTML;
		document.body.appendChild(newPage);

		if (pageTemplateId == "template.text_card_editor") {
			document.addTextCardEditor(pageId);
		}

		return newPage;
	}

	function switch_page(pageId) {
		var page = find_page_by_id(pageId);
		if (page == null) page = create_page(pageId, "template.text_card_editor");

		show_page(pageId);
	}

	// Hiding body before loaded to make sure nothing is visible until everything is parsed
	var body_hidden = document.createElement('style');
	body_hidden.innerHTML = "body { display: none; }";
	document.getElementsByTagName('head')[0].appendChild(body_hidden);

	window.addEventListener("load", function () {
		var hashchange = function () {
			if (document.location.hash == "" || document.location.hash == "#") {
				var pages = document.body.getElementsByTagName(Document.prototype.pager.config.tags.page);
				document.location.hash = "index";//pages[0].id;
			}

			var pageId = document.location.hash;
			if (pageId.charAt(0) == "#") pageId = pageId.substring(1);
			if (pageId.charAt(0) == "/") pageId = pageId.substring(1);

			switch_page(pageId);
		};

		window.addEventListener("hashchange", hashchange);
		// Running the onHashChange manually on the first load
		hashchange();

		// The pages are parsed and selected, now we can show the body
		document.body.style.display = "block";
	});

	// Declaring the interface
	var pager = {
		find_page_by_id: find_page_by_id,
		show_page: show_page,
		find_page_template_by_id: find_page_template_by_id,
		create_page: create_page,

		config: {
			tags: {
				page: 'page',
				page_template: 'page-template'
			}
		}
	}

	Document.prototype.pager = pager;
})();

// Global scope object
this.pager = Document.prototype.pager;
