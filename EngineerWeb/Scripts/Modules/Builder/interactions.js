﻿var _selItem = null;
var _previewTemplates = [];
var _propTemplates = [];
var _snippetsTemplates = [];
var count = 0;
var order = 0;
var graph = new joint.dia.Graph;
var paper;
var rect;
var actCounter = 1;
var selected;
function initInteractions() {
    loadTemplates();
    $("#preview").sortable({
        revert: false,
        receive: function (event, ui) { //Preview Area can receive elements either from Toolbox or Fieldset
            var isItemFromToolbox = ui.sender.closest("#toolbox").length == 1;
            if (isItemFromToolbox) {
                //var type = ui.item.data("type");
                var currentItem = $("#preview .toolboxItem");
                addToolboxItemToPreviewArea(currentItem);
            }
        } // end receive function
    }); // end sortable

    $(".toolboxItem").draggable({
        connectToSortable: ".sortable",
        revert: false,
        helper: "clone"
    }); // end draggable
    $(".toolboxItem").disableSelection();
}
function loadTemplates()
{
    var templates = JSON.parse($("#" + templatesClientId + "").val());
    _previewTemplates = templates["Preview"];
    _snippetsTemplates = templates["Snippets"];
    $.each(templates['Prop'], function (key, val) {
        var tempFn = doT.template(val, undefined, _snippetsTemplates);
        _propTemplates[key] = tempFn;
    });
    
}
function addToolboxItemToPreviewArea(currentItem) {
    var type = currentItem.data("type");
    currentItem.removeClass("col-xs-6");
    currentItem.removeClass("toolboxItem");
    currentItem.addClass("previewItem");
    currentItem.html(_previewTemplates[type]);
}
function saveCurrentSelItemProp() {
    if (_selItem != null) {
        var jsonProp = [];
        $("#prop :input").serializeArray().map(function (x) {
            jsonProp[x.name] = x.value;
        });
        _selItem.data("prop", jsonProp);
    }
}

function deleteSelContol() {
    if (selected != null)
        selected.remove();
}

function changeActivityLabel($input)
{
    if (selected != null)
        paper.getModelById(selected.id).prop("attrs/text/text", $($input).val());
}
function save(type) {

    //type = typeof type !== 'undefined' ? type : 'create';

    var result = $('#engineerForm').valid();
    if (result == false) {
        errorAlert('Your Form contains errors, please fix them before saving');
        return;
    }
    if (($("#UserStoriesList").val() == null || $("#UserStoriesList").val() == "" ) && $("#diagramID").val() == "")
    {
        errorAlert("Please select at least 1 user story");
        return;
    }
    if ($("#diagramName").val() == "")
    {
        errorAlert("Please give a name to diagram");
        return;
    }

    // Save currently open properties menu
    //saveCurrentSelItemProp();
    var graphStr = JSON.stringify(graph);
    var graphJSON = JSON.parse(graphStr);
    var svgDoc = paper.svg;
    var serializer = new XMLSerializer();
    var svgString = serializer.serializeToString(svgDoc);
    var diagram = {
        diagram:{
            name: $("#diagramName").val(),
            userStories:$("#UserStoriesList").val(),
            graph: graphStr,
            svg: svgString,
            Id: $("#diagramID").val(),
            userStoryId: $("#UserStoryID").val()
        }
        
    };
    
    $.ajax({
        url: $("#diagramID").val() == "" ? saveURL : updateURL,
        type: 'POST',
        data: JSON.stringify(diagram),
        contentType: 'application/json; charset=utf-8',
        dataType: 'json',
        success: function (formId) {
            successAlert('Your Diagram have been successfully Saved');

            if ($("#diagramID").val() == "") {
                window.location.href = 'List';
            }
        },
        error: function (jqXHR, err) {
            var status = jqXHR.status;
            if (status == 403 || status == 401)
                warningAlert("Session Expired please relogin");
            else
                errorAlert(jqXHR.responseText);
        }
    });
}
function openRenameFormDialog() {
    $('.modalDialog').load('dialogs/rename-form.html', function (modal) {
        $('#modal').modal('show');
    });
}
function loadGraph()
{
    var graphStr = $("#diagramGraph").val();
    if (graphStr != "")
    {
        var graphJSON = JSON.parse(graphStr);
        if (paper == null) {
            paper = new joint.dia.Paper({
                el: $('#preview'),
                width: 600,
                height: 1000,
                model: graph,
                gridSize: 1
            });
        }
        graph = graph.fromJSON(graphJSON);
        var lastLoaded = false;
        if (graph.attributes.cells.models.length > 0)
        {
            var parentItem = $("#preview");
            for (var i = 0; i<= graph.attributes.cells.models.length - 1; i++)
            {
                if (graph.attributes.cells.models[i].attributes.type == "basic.Rect")
                {
                    rect = graph.attributes.cells.models[i];
                    var selItem = $($("#preview").children().find(".viewport").children()).filter(function () { return $(this).attr("model-id") == rect.id; });
                    if ($(selItem).length > 0) {
                        var ctrlName = "ctrl" + (count++);
                        var json = {
                            "ctrlName": ctrlName,
                            "order": "" + (order++),
                            "label": paper.getModelById(rect.id).attributes.attrs.text.text
                        };
                        $(selItem).data("prop", json);
                        $(selItem).data("type", "activity");
                    }
                }
            }
            paper.on('cell:pointerdown', function (cellView, evt, x, y) {
                saveCurrentSelItemProp();
                selected = cellView.model;
                var activityId = cellView.model.id;
                var selItem = $($("#preview").children().find(".viewport").children()).filter(function () { return $(this).attr("model-id") == activityId; });
                if (selItem.length > 0) {
                    var type = $(selItem).data("type");
                    _selItem = $(selItem);
                    var tempFn = _propTemplates[type];
                    var result = tempFn(_selItem.data("prop"));
                    $("#prop").html(result);
                    $("#preview .selectedControl").removeClass("selectedControl");
                    $(this).addClass("selectedControl");
                }
            });
        }
        

        $("#renameMenu").hide();
    }
}
$(document).ready(function () {
    initInteractions();
    loadGraph();
    $("#dialogNameTitle").text($("#diagramName").val());
    $(document).on('click', '#toolbox .btn', function (e) {
        e.preventDefault();
    });
    $("#renameBtn").on("click", function () {
        if($('#engineerForm').valid())
        {
            $("#dialogNameTitle").text($("#diagramName").val());
            $('#renameModal').modal('hide');
        }
    });
    
}); //end ready