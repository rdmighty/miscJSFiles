import { AfterContentChecked, Directive, ElementRef, Injector, Input, DoCheck, OnChanges, SimpleChanges } from '@angular/core';
import * as moment from 'moment';
import { NgModel } from '@angular/forms';

@Directive({
    selector: '[ngModel][bs-ref]',
    providers: [NgModel]
})
export class BsRef implements OnChanges{
    @Input('bs-ref') bsRef: moment.Moment; //date
    hostElement: ElementRef;
    count: number = 1;

    constructor(
        injector: Injector,
        private ngModel: NgModel,
        private _element: ElementRef,
    ) {
        this.hostElement = _element;
    }

    ngOnChanges(changes: SimpleChanges): void {
        if(changes.bsRef.currentValue){
            moment.isMoment(changes.bsRef.currentValue) && this.ngModel.update.emit(changes.bsRef.currentValue.toDate());
            changes.bsRef.currentValue == "Invalid Date" && this.ngModel.update.emit(null);
        }              
    }
}
