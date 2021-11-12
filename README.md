# EQD2/BED converter
This Varian ESAPI script converts nominal dose distributions to EQD2 or BED dose distributions. It should be used only for experimentation purposes.

![image](image_asc2.png)

## Setup

To use the script, you must compile it on your system. You should be able to open the project with Visual Studio 2019 Community Edition. Open the .sln file. 
The script was developed for Eclipse version 15.6. It may not work with other versions of Eclipse or Varian ESAPI.

1. You will need to restore NuGet package for compilation: Evil-Dicom version 2.0.4, and OxyPlot. Right click the solution -> Restore NuGet packages.
2. Don't forget the references to Varian dlls.
3. Compile as Release for x64.

## How to use the script

Because it is not possible to modify the dose matrix inside ESAPI, a trick is needed. The dose distribution is modified outside Eclipse with EvilDICOM, and is then imported back to Eclipse:

1. Copy the plan that you would like to convert to EQD2/BED. 
2. Export the plan dose distribution of the copied plan to an external dicom file.
3. Delete the copy of the plan in Eclipse.
4. Run the script on the original plan. Define alpha/beta and click the button. Pick the exported dicom file when asked.
5. When you see the message "Done!", close the script.
6. Create a new plan in Eclipse. Do not add fields to the plan, but make sure that the StructureSet assigned is the same as in the original plan.
7. Import the dicom dose file and attach it to the created empty plan. That is all. At the end you will have a plan without any fields, but with a valid dose distribution.

## How to preview the conversion

You can also preview the conversion without actually saving it to dicom. Edit the alpha/beta table and click **Preview**. A new window will pop up. On the left side you can select which structures you would like to see superimposed on the dose plot. The left and right OxyPlot regions will show the original and modified dose distributions. Here is a list of hints:

* You can scroll both dose distributions with the scroll wheel or by moving the bottom slider left and right.
* The right (modified) dose distribution is linked with the left dose distribution, but not vice-versa. To pan the distribution press the middle mouse button or use the keyboard arrow keys. To region-zoom the distribution right click and draw a rectangle. To reset the display double right click. To zoom in/out press the CTRL button and scroll.
* Left mouse button displays the tracker. The tracker is synced between both plots.
* Both distributions use the same colormap. You can change the range of the colors by moving the slider on the right side, or by entering new values in the text boxes. When you type in a new value you must hit the Enter key.
* You can change the selection of structures in-vivo. If you press the CTRL key, you can select multiple structures.

Dose is displayed in Grays. If you don't see the correct unit, send me a message. It should be easy to fix the script for other units. More is to come when I find the time.

Another thing, the displayed dose in the Preview window may not exactly match with the absolute value of dose which is inserted into dicom. The reason is that I did not use the internal function to convert raw pixel values into user units because it works extremely slow. Instead, I calculated the dose scaling factor (equal to the one in dicom) by dividing the max dose in the 3D distribution that Eclipse returns quickly and the max integer value in the raw dose matrix. 

![image](image_asc3.png)


## Details

1. The calculation is performed with the well known formulas: EQD2 = D ( a/b + D/n) / (a/b + 2) and BED = D (1 + D / ( n a/b)). The third option, Multiply by a/b, is for testing purposes, ie. it simply multiples each voxel value with a/b.
2. The accuracy of conversion equals the width of the dose matrix box. Do some testing to see how it works. The scanning for voxels inside structures is done only in the X direction.
3. When you define a/b for each structure, you have to decide how the script will deal with overlapping regions. Using "Ascending": structures will be ordered in ascending order of a/b. Meaning that the structure with lower a/b will have all voxels overridden with new values, but the overlapping part of structures with higher a/b will not have values overridden for those voxels that are inside structures with lower a/b. For "Descending" the opposite applies. See image below.
4. If you need better accuracy, calculate the original plan with smaller dose box width.
5. The conversion should (only) work for HFS, HFP, FFS, FFP orientations. If you are summing up plans based on different registered images, the result may be wrong. I haven't had time to test this.


![image](image_asc.png)


## Log

* (12.11.2021) Added the preview window.


## Important note

**Before using this program see the [licence](https://github.com/brjdenis/VarianESAPI-EQD2Converter/blob/master/LICENSE) and make sure you understand it. The program comes with absolutely no guarantees of any kind.**

```
THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```


## LICENSE

Published under the MIT license. 