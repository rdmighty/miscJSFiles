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
