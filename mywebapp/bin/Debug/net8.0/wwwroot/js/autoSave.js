function setupAutoSave() {
    const autoSaveInterval = 60000; // 60 seconds
    let lastSavedContent = '';

    setInterval(async () => {
        const codeEditor = document.getElementById('SubmittedCode');
        const currentContent = codeEditor.value;

        // Only save if content has changed
        if (currentContent && currentContent !== lastSavedContent) {
            try {
                const response = await fetch(`/Dashboard?handler=AutoSave`, {
                    method: 'POST',
                    headers: {
                        'Content-Type': 'application/json',
                        'RequestVerificationToken': document.querySelector('input[name="__RequestVerificationToken"]').value
                    },
                    body: JSON.stringify({
                        StudentId: document.getElementById('StudentId').value,
                        GroupId: parseInt(document.getElementById('Group').value),
                        QuestionIndex: parseInt(document.getElementById('Question').value),
                        Code: currentContent
                    })
                });

                const result = await response.json();
                if (result.success) {
                    lastSavedContent = currentContent;
                    console.log('Auto-saved successfully at:', new Date().toLocaleTimeString());
                }
            } catch (error) {
                console.error('Auto-save failed:', error);
            }
        }
    }, autoSaveInterval);
}

async function testAutoSave() {
    const statusElement = document.getElementById('autoSaveStatus');
    const testCode = "// Test autosave " + new Date().toISOString();
    
    try {
        statusElement.textContent = "Testing autosave...";
        
        const response = await fetch('/Dashboard?handler=AutoSave', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'RequestVerificationToken': document.querySelector('input[name="__RequestVerificationToken"]').value
            },
            body: JSON.stringify({
                StudentId: document.getElementById('StudentId').value,
                GroupId: parseInt(document.getElementById('Group').value),
                QuestionIndex: parseInt(document.getElementById('Question').value),
                Code: testCode
            })
        });

        const result = await response.json();
        if (result.success) {
            statusElement.textContent = "✅ Autosave test successful";
            statusElement.style.color = "green";
        } else {
            statusElement.textContent = "❌ Autosave test failed: " + result.message;
            statusElement.style.color = "red";
        }
    } catch (error) {
        statusElement.textContent = "❌ Autosave test error: " + error.message;
        statusElement.style.color = "red";
    }
    
    // Clear status after 3 seconds
    setTimeout(() => {
        statusElement.textContent = "";
    }, 3000);
}

document.addEventListener('DOMContentLoaded', setupAutoSave);