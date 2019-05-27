import * as _ from 'underscore';
import * as moment from 'moment';
import { Observable } from 'rxjs';

// Declarations
interface IEntityDateOnlyMapper{
    props: string[];
}

abstract class EntityDateOnlyMapper implements IEntityDateOnlyMapper{
    props: string[] ;    
}

var maps: {[key:string]: EntityDateOnlyMapper} = {};

class TaskDateOnlyMapper extends EntityDateOnlyMapper{
    constructor(){
        super();
        this.props = ['startDate', 'finishDate'];
    }    
}

class ProjectDateOnlyMapper extends EntityDateOnlyMapper{
    constructor(){
        super();
        this.props = ['startDate', 'endDate'];
    }    
}

// Definitions
maps["VisualTask"] = new TaskDateOnlyMapper();
maps["WorkplanTask"] = maps["VisualTask"];
maps["ProjectplanTask"] = maps["VisualTask"];
maps["RequestplanTask"] = maps["VisualTask"];
maps["CreateOrEditWorkplanTaskDto"] = maps["VisualTask"];
maps["CreateorEditProjectplanTaskDto"] = maps["VisualTask"];
maps["CreateOrEditProjectDto"] = new ProjectDateOnlyMapper();

// Utility
var utils = {
    readValueAtNav: function(obj, nav): any{
        if (Array.isArray(nav)) {
            if (nav.length == 1 && obj)
                return obj[nav[0]];
            else if (nav.length > 1 && obj)
                return utils.readValueAtNav(obj[nav[0]], nav.splice(1));
            else
                return null;
        }
        else
            return utils.readValueAtNav(obj, nav.split('.'));
    },
    assignValueAtNav: function(obj, nav, value){
        if (Array.isArray(nav)) {
            if (nav.length == 1 && obj)
                return obj[nav[0]] = value;
            else if (nav.length > 1 && obj)
                return utils.assignValueAtNav(obj[nav[0]], nav.splice(1), value);            
        }
        else
            return utils.assignValueAtNav(obj, nav.split('.'), value);
    },
    objectKeysWithNav: function(obj, suffix = null, arr = null, includeOnlyMomentProp = true){
		arr = arr || [];
        suffix = suffix ? suffix: '';
        
        if(obj instanceof Object){
            var keys = Object.keys(obj);
            for(var i = 0; i < keys.length; i ++){
                if(obj[keys[i]] instanceof Object && !moment.isMoment(obj[keys[i]])){
                    utils.objectKeysWithNav(obj[keys[i]],suffix ? suffix + "." + keys[i]: keys[i], arr);            
                }else{	
                    if(!includeOnlyMomentProp || includeOnlyMomentProp && obj[keys[i]] instanceof moment)				
                        arr.push(suffix ? suffix + "." + keys[i]: keys[i]);            
                }
            }
        }
        return arr;
    }
};

// DaetOnly Apis
export class DateOnlyApi{
    static callApi(fnName: string, args?: any[]): Observable<any>{
        DateOnlyApi.dateOnly(false, args);

        return new Observable((obs: any) => {
            this[fnName].apply(this, args).subscribe(result => {
                DateOnlyApi.dateOnly(true, [result])
                obs.next(result);
                obs.complete();
            });
        });
    }

    static dateOnly(incoming: boolean, args?: any | any[]){
        if(args){
            if(args instanceof Array){
                args.forEach(item => {
                    DateOnlyApi.dateOnly(incoming, item);
                });
            }else{
                if(args instanceof Object){                    
                    var _map = maps[args.constructor.name];

                    if(_map){
                        _map.props.forEach(prop => {
                            if(incoming)
                                utils.assignValueAtNav(args, prop, utils.readValueAtNav(args, prop).maintainUtc());
                            else
                                utils.assignValueAtNav(args, prop, utils.readValueAtNav(args, prop).startOfTheDay()); 
                        });
                    }else{
                        var keys = Object.keys(args);
                        
                        _.forEach(keys, key => {
                            if((args[key] instanceof Object) && !(args[key] instanceof moment))
                                DateOnlyApi.dateOnly(incoming, args[key]);
                        });                        
                    }                                        
                }
            }            
        }                
    }    
}
