import { Component, EventEmitter, Input, Output } from "@angular/core";
import { SecurityService } from "../../../../security/shared/security.service";
import { DischargeSummaryViewModel } from "../discharge-summary-view-model";

@Component({
    selector: 'sch-discharge-summary-view-template',
    templateUrl: './sch-discharge-summary-template.html'
})

export class SCHDischargeSummaryTemplateComponent {
    @Input('viewData')
    dsVM: DischargeSummaryViewModel;

    @Input('ShowDoctorsSignatureImage')
    public ShowDoctorsSignatureImage: boolean = false;

    @Input('IsFinalSubmited')
    public IsFinalSubmited: boolean;

    public isEditMode: boolean = false;

    @Output('EditModeFromSCHView') SendData: EventEmitter<any> = new EventEmitter<any>();
    hasEditDischargeSunnaryPermission: boolean = false;
    hideBtn: boolean = false;
    test: DischargeSummaryViewModel;

    constructor(public securityService: SecurityService) {
        this.hasEditDischargeSunnaryPermission = this.securityService.HasPermission('btn-edit-discharge-summary-after-final-submit');

    }
    ngOnInit() {
        this.test = this.dsVM;
    }

    print() {
        let popupWinindow;
        var printContents = document.getElementById("printpage").innerHTML;
        popupWinindow = window.open('', '_blank', 'width=600,height=700,scrollbars=no,menubar=no,toolbar=no,location=no,status=no,titlebar=no');
        popupWinindow.document.open();

        let documentContent = "<html><head>";
        documentContent += '<link rel="stylesheet" type="text/css" href="../../themes/theme-default/DanphePrintStyle.css"/>';
        documentContent += '<link rel="stylesheet" type="text/css" href="../../themes/theme-default/DanpheStyle.css"/>';
        documentContent += '<link rel="stylesheet" type="text/css" href="../../../assets/global/plugins/bootstrap/css/bootstrap.min.css"/>';
        documentContent += '<link rel="stylesheet" type="text/css" href="../../themes/theme-default/Danphe_ui_style.css"/>';


        documentContent += `<style>
        .img-responsive{ position: static;left: 2px;top: 10px;}
        .qr-code{position: absolute; left: 1001px;top: 9px;}
        .invoice-print-header .col-md-2 {
            width: 20%;
            float: left;
        }
        .invoice-print-header .col-md-8 {
            width: 60%;
            float: left;
        }
        .sub-main-cls, ul.adviceSubList li {
            width: 50% !important;
            display: inline-block !important;
            padding: 1%;
        }
        ul.adviceSubList li {
             flex: 0 0 47%;
        }
        .sub-main-cls-fullwidth, ul.adviceSubList li .sub-main-cls {
            width: 100% !important;
            display: block !important;
        }
        .dsv-div .left-panel .patient-hdr-label, .left-panel .patient-hdr-label {
            display: inline-block;
            width: 50%;
        }
        .dsv-div .dsv-div .left-panel .right {
            padding-left: 15%;
          }
        .left-panel .patient-hdr-label.signature, .dr-signature-list .patient-hdr-label {
            max-width: 400px;
            width: 100%;
            display: block;
            font-size:13px;
        }
        .left-panel .patient-hdr-label b:before,
        .p-relative b:before {
            display: none !important;    
        }
        .sub-main-cls-default {
            width: 100% !important;
        }

        .sub-main-cls-default .p-relative {
            position: relative;
        }

        .sub-main-cls-default strong {
            
            font-weight: bold;
            margin-right:10px;
        }
        .left-panel .patient-hdr-label b:before, .p-relative b:before {
            display: none;
        }

        .lab-test-list li::before {
            transform: rotate(45deg);
            height: var(--height);
            width: var(--width);
            border-bottom: var(--borderWidth) solid var(--borderColor);
            border-right: var(--borderWidth) solid var(--borderColor);
            display: block;
            position: absolute;
            top: 7px;
            left: 0;
        }
        .right-panel {
            font-weight: normal;
        }
        ol {
            padding-left: 20px!important;
        }
        .lab-test-list ol {
            padding-left: 20px!important;
        }

      </style>`;



        documentContent += '</head>';
        documentContent += '<body onload="window.print()">' + printContents + '</body></html>'
        popupWinindow.document.write(documentContent);
        popupWinindow.document.close();
    }

    public EditRecord() {
        this.isEditMode = true;
        this.SendData.emit(this.isEditMode);

    }


}