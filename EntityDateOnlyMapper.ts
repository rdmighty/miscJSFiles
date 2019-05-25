import * as moment from 'moment';

Date.prototype.addTimezoneOffset = function(){
    if(this){
        var _mThis = moment(this);
        return moment(this).add(_mThis.utcOffset()/60, 'hours').toDate();
    }

    return this;
};

Date.prototype.startOfTheDay = function(){
    if(this)
        return this.hours(0).minutes(0).seconds(0).add(this.utcOffset()/60, 'hours');    

    return this;
};

moment.prototype.addTimezoneOffset = function(){
    if(this){
        return this.add(this.utcOffset()/60, 'hours').toDate();
    }
}

moment.prototype.startOfTheDay = function(){
    if(this){
        var _moment = moment(this);
        return _moment.hours(0).minutes(0).seconds(0).add(_moment.utcOffset()/60, 'hours').toDate();
    }
}

import * as _ from 'underscore';
import { Observable } from 'rxjs';

abstract class EntityDateOnlyMapper{
    outgoing: string[];    
    incoming: string[];
    entity: any;
}

var maps: {[key:string]: EntityDateOnlyMapper} = {};

class VisualTaskDateOnlyMapper extends EntityDateOnlyMapper{
    constructor(){
        super();
        this.entity = "VisualTask"
        this.outgoing = ['startDate', 'finishDate'];
        this.incoming = ['transition'];
    }
}

maps["VisualTask"] = new VisualTaskDateOnlyMapper();

export class DateOnlyApiCaller{
    static callApi(fn: (...any) => any, args?: any[]): Observable<any>{
        DateOnlyApiCaller.dateOnly(false, args);

        return new Observable((obs: any) => {
            fn.apply(this, args).subscribe(result => {
                DateOnlyApiCaller.dateOnly(true, result)
                obs.next(result);
                obs.complete();
            });
        });
    }

    static dateOnly(incoming: boolean, args?: any[]){
        if(args){
            args.forEach(item => {
                if(item instanceof Array){
                    DateOnlyApiCaller.dateOnly(incoming, item);
                    return;
                }

                if(item instanceof Object){
                    var _map = maps[item.constructor.name];
        
                    var keys = Object.keys(item);
        
                    _.forEach(keys, key => {
                        if(incoming){
                            if(_map.incoming.indexOf(key) != -1){
                                item[key] = item[key].addTimezoneOffset();
                            }
                        }else{
                            if(_map.outgoing.indexOf(key) != -1){
                                item[key] = item[key].startOfTheDay();
                            }
                        }                        
                    });
                }
            });
        }                
    }
}
