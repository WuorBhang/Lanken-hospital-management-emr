import { Component, ElementRef, EventEmitter, Input, OnInit, Output, ViewChild } from '@angular/core';
import { MessageboxService } from '../../../shared/messagebox/messagebox.service';
import { InventoryBLService } from '../../shared/inventory.bl.service';
import { InventoryService } from '../../shared/inventory.service';
import { StockModel } from '../../shared/stock.model';
import * as XLSX from 'xlsx';
import * as moment from 'moment';


@Component({
  selector: 'app-stock-reconciliation',
  templateUrl: './stock-reconciliation.component.html',
  styleUrls: ['./stock-reconciliation.component.css']
})
export class StockReconciliationComponent implements OnInit {

  @Input('stock-list') stockList: Array<StockModel> = new Array<StockModel>();
  @Input('StoreId') StoreId: number = null;
  @Output('call-back-conciliation-popup-close') callBackConciliationPopUpClose: EventEmitter<Object> = new EventEmitter<Object>();
  openExcelSheet: boolean = false;
  showAllStock: boolean = false;
  showSpinner: boolean = false;
  openPopUp: boolean = false;
  disableBtn: boolean = true;
  @ViewChild('inputFile') inputFile: ElementRef;
  isExcelFile: boolean;
  reconciledStocks: StockModel[] = [];
  reconciledStocksWithQuantityChanges: any[] = [];
  confirmPopUp: boolean = false;
  loading: boolean = false;

  constructor(public msgBoxServ: MessageboxService, public inventoryBLservice: InventoryBLService, public inventoryservice: InventoryService,) { }

  ngOnInit() {
  }
  ClosePopUp() {
    this.disableBtn = true;
    this.reconciledStocksWithQuantityChanges = [];
    this.reconciledStocks = [];
    this.callBackConciliationPopUpClose.emit();
  }
  CloseConfirmationPopUp() {
    this.confirmPopUp = false;
    this.reconciledStocksWithQuantityChanges = [];
    this.reconciledStocks = [];
    this.inputFile.nativeElement.value = '';
    this.showAllStock = false;
  }

  ExportStocksForReconciliationToExcel() {
    this.showSpinner = true;
    this.inventoryBLservice.ExportStocksForReconciliationToExcel(this.StoreId)
      .finally(() => { this.showSpinner = false; })
      .subscribe(res => {
        let blob = res;
        let a = document.createElement("a");
        a.href = URL.createObjectURL(blob);
        a.download = "InventoryStockReconciliation_" + moment().format("DD-MMM-YYYY_HHmmA") + '.xlsx';
        document.body.appendChild(a);
        a.click();
      },
        err => {
          console.error(err);
          this.msgBoxServ.showMessage('Failed', [`Download Failed. Check Console.`]);
        });

  }

  onChange(evt) {
    const target: DataTransfer = <DataTransfer>(evt.target);
    this.isExcelFile = !!target.files[0].name.match(/(.xls|.xlsx)/);
    target.files.length > 0 ? this.disableBtn = false : this.disableBtn = true;
    if (target.files.length > 1) {
      this.inputFile.nativeElement.value = '';
    }
    if (this.isExcelFile) {
      const reader: FileReader = new FileReader();
      reader.onload = (e: any) => {
        //read workbook
        const bstr: string = e.target.result;
        const wb: XLSX.WorkBook = XLSX.read(bstr, { type: 'binary' });
        //grab first sheet
        const wsname: string = wb.SheetNames[0];
        const ws: XLSX.WorkSheet = wb.Sheets[wsname];
        //save data
        this.reconciledStocks = XLSX.utils.sheet_to_json(ws);
      };
      reader.readAsBinaryString(target.files[0]);
    }
    else {
      this.inputFile.nativeElement.value = '';
    }
  }

  FindDifference() {
    for (let i = 0; i <= this.stockList.length - 1; i++) {
      for (let j = i; j <= this.reconciledStocks.length - 1; j++) {
        if (i == j) {

          if (this.stockList[i].AvailQuantity != this.reconciledStocks[j].AvailQuantity) {
            this.msgBoxServ.showMessage('Notification', ['Please Select Latest File To Import']);
            return;
          }
          if (this.stockList[i].AvailQuantity != this.reconciledStocks[j].NewQuantity) {
            this.reconciledStocksWithQuantityChanges.push(this.reconciledStocks[j]);
          }
        }
      }
    }
    this.confirmPopUp = true;
  }
  UpdateReconciledStockFromExcelFile() {
    this.loading = true;
    this.inventoryBLservice.UpdateReconciledStockFromExcelFile(this.reconciledStocksWithQuantityChanges).finally(() => {
      this.loading = false;
      this.confirmPopUp = false;
    }).subscribe(res => {
      if (res.Status == "OK") {
        this.reconciledStocksWithQuantityChanges = [];
        this.msgBoxServ.showMessage('success', ['Stock Updated Successfully']);
        this.callBackConciliationPopUpClose.emit();
      }
      else {
        this.msgBoxServ.showMessage('Failed', [`Stock Updation Failed! ${res.ErrorMessage}`]);
        this.reconciledStocksWithQuantityChanges = [];
      }
    })
  }
  ViewAllStock() {
    this.showAllStock = true;
  }
  HideAllStock() {
    this.showAllStock = false;
  }

}
