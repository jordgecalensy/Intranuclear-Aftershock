using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class flashlight : MonoBehaviour
{
    public Light light_fonarik; //свет фонарика
    public bool Vkl; //включен или выключен фонарик
    public bool OnChangeColor=true; //можно ли включать светофильтр
    public bool OnSpotRange=true; //можно ли изменять пучек света
    public bool OnStrobos=true; //можно ли включать страбоскоп
    public float time_miganieVkl; //интервал мигания
    public float minAngle=30f; //минимальная длина луча
    public float maxAngle=80f; //максимальная длина луча
    public float speedAngle=5f; //скорость изменения длины луча
    public float speedRange=2f; //скорость изменения пучка света
    public float minRange=30f; //минимальный размер пучка света
    public float maxRange=80f; //максимальный размер пучка света
    private Color colorStan; //базовый цвет фонарика
    public Color colorSvetofiltr; //цвет светофильтр
    public AudioSource asF; //компонент аудиосорс для звука
    private bool activeStrob=false; //активен ли страбоскоп
    private bool strabmigan=false; //активно ли мигание страбоскопа
    private float time_fiks_miganie; //фикисрованное время мигания
    private bool colChande = false; //изменен ли цвет
    // Start is called before the first frame update
    void Start()
    {
        colorStan=light_fonarik.color;//задаем базовый цвет света исходя из выставленного цвета в инспекторе
        Proverka(); //проверяем включен или выключен фонарик при старте
        time_fiks_miganie=time_miganieVkl; //синхронизируем время мигания 
        
    }

    // Update is called once per frame
    void Update()
    {
        if(OnChangeColor==true){
            ChangeColor();//замена цвета (светофильтр)
        }
        if(OnSpotRange==true){
            SpotRange();//Изменение пучка света (области освещения)
        }
        
        //включаем и выключаем фонарик
        FlashLightOnOF();
    }
   
   
   
   //включение и выключение фонарика
    public void FlashLightOnOF(){
         if (Input.GetKeyDown (KeyCode.F) && Vkl == false) // если нажали клавишу F и фонарик выключен
		{
			
			light_fonarik.enabled=true; // включаем свет
            Vkl = true; // указываем, что фонарик включен
            asF.Play();// проигрываем звук
		           
		}
        //выключаем фонарик
		else if (Input.GetKeyDown (KeyCode.F) && Vkl == true) // если нажали клавишу F и фонарик включен
		{
			
			light_fonarik.enabled=false; //выключаем свет
            Vkl = false; // указываем, что фонарик выключен
            asF.Play();// проигрываем звук
            activeStrob=false; //октлючаем страбоскоп
			
		}
       else if(Input.GetKeyDown (KeyCode.T) && Vkl == true && activeStrob==false && OnStrobos==true ){//включаем страбоскоп по нажатию кнопки T
                activeStrob=true;
                asF.Play();
            }
            else if(Input.GetKeyDown (KeyCode.T) && Vkl == true && activeStrob==true && OnStrobos==true){ ////выключаем страбоскоп по нажатию кнопки T
                activeStrob=false;
                light_fonarik.enabled=true;//включаем свет фонарика, так как при выключение страбоскопа, может отключатся свет фонарика
                asF.Play();
            }
        else if (activeStrob==true){//активируем страбоскоп
            stroboscop();//включаем страбоскоп
        }
    }
  
  
  
  
    //Проверяем статус фонарика и включаем или выключаем
    public void Proverka() {
       if(Vkl == true)
        {
        light_fonarik.enabled=true;
        }
        
        else if(Vkl == false)
        {
        light_fonarik.enabled=false;
        }
    }
   
   
   
    //стробоскоп
    public void stroboscop(){              
        time_miganieVkl-=Time.deltaTime;//уменьшаем вреям

        if(time_miganieVkl<0&&strabmigan==true){//выключаем свет
            light_fonarik.enabled=false;
            time_miganieVkl=time_fiks_miganie;
            strabmigan=false;
        }

        else if(time_miganieVkl<0&&strabmigan==false){//включаем свет
            light_fonarik.enabled=true;
            time_miganieVkl=time_fiks_miganie;
            strabmigan=true;
        }
    }
   
   
    //изменение пучка света
    public void SpotRange(){
        if(Input.GetAxis("Mouse ScrollWheel") > 0f ) { // Двигаем колесико мышки вверх
        light_fonarik.spotAngle=light_fonarik.spotAngle+speedAngle;//увеличиваем длину луча
        light_fonarik.range=light_fonarik.range-speedRange; //уменьшаем пучек света
        
        if(light_fonarik.spotAngle>maxAngle){//Если длина луча больше максимального значения
        light_fonarik.spotAngle=maxAngle;//длину луча фиксируем на максимальное значение
        }

        if(light_fonarik.range<minRange){//Если пучек света меньше минимального значения
        light_fonarik.range=minRange;//пучек света фиксируем на минимальном значении
        }
        } 
       
       
        else if(Input.GetAxis("Mouse ScrollWheel") < 0f ) { // // Двигаем колесико мышки вниз
        light_fonarik.spotAngle=light_fonarik.spotAngle-speedAngle;//уменьшаем длину луча
        light_fonarik.range=light_fonarik.range+speedRange; //увеличиваем пучек света
        
        if(light_fonarik.spotAngle<minAngle){//Если длина луча меньше минимального значения
        light_fonarik.spotAngle=minAngle;//длину луча фиксируем на минимальном значении
        }

        if(light_fonarik.range>maxRange){//Если пучек света больше максимального значения
        light_fonarik.range=maxRange;//пучек света фиксируем на максимальном значении
        }
        }
     }
    
    
    //измениение цвета фонарика (светофильтр)
    public void ChangeColor(){
        if(Input.GetKeyDown (KeyCode.C) && Vkl == true && colChande==false){//изменяем цвет фонаря на светофильтр
        light_fonarik.color=colorSvetofiltr;
        colChande=true;
        asF.Play();
        }
        else if(Input.GetKeyDown (KeyCode.C) && Vkl == true && colChande==true){//изменяем цвет фонаря на базовый
        light_fonarik.color=colorStan;
        colChande=false;
        asF.Play();
        }
    }

}
