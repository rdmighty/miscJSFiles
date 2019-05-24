import * as moment from 'moment';

Date.prototype.toStartOfTheDay = function(){
    if(this != null){
        var isoString = this.toISOString();

        var arr = isoString.split(/[-T]+/)
        return new Date(new Date(arr[0] + '-' + arr[1] + '-' + arr[2]).setHours(0, 0, 0, 0) - (moment().utcOffset() * 60 * 1000)); 
    }                 

    return this;
};

moment.prototype.toStartOfTheDay = function(){
    if(this != null){
        var isoString = this.toISOString();
        var hrsToAdd = -this.utcOffset()/60;

        var arr = isoString.split(/[-T]+/)
        return moment(arr[0] + '-' + arr[1] + '-' + arr[2]).add(hrsToAdd, 'hours');
    }   
    
    return null;
}

import * as _ from 'underscore';
import { Observable } from 'rxjs';

interface EntityDateOnlyMapper{
    props: string[];
    entity: any;
}

var maps: {[key:string]: EntityDateOnlyMapper} = {};

class VisualTaskDateOnlyMapper implements EntityDateOnlyMapper{
    props: string[] ;    
    entity: any;

    constructor(){
        this.props = ['startDate', 'finishDate'];
        this.entity = "VisualTask"
    }
}

maps["VisualTask"] = new VisualTaskDateOnlyMapper();


export class DateOnlyApiCaller{
    static callApi(fn: (...any) => any, args?: any[]): Observable<any>{
        DateOnlyApiCaller.dateOnly(args);

        return new Observable((obs: any) => {
            fn.apply(this, args).subscribe(result => {
                DateOnlyApiCaller.dateOnly(result)
                obs.next(result);
                obs.complete();
            });
        });
    }

    static dateOnly(args?: any[]){
        if(args){
            args.forEach(item => {
                if(item instanceof Array){
                    DateOnlyApiCaller.dateOnly(item);
                    return;
                }

                if(item instanceof Object){
                    var _map = maps[item.constructor.name];
        
                    var keys = Object.keys(item);
        
                    _.forEach(keys, key => {
                        if(_map.props.indexOf(key) != -1){
                            item[key] = item[key].toStartOfTheDay();
                        }
                    });
                }
            });
        }                
    }
}
